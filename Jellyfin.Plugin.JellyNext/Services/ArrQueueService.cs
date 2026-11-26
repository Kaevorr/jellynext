using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyNext.Models.Common;
using Jellyfin.Plugin.JellyNext.Models.Radarr;
using Jellyfin.Plugin.JellyNext.Models.Sonarr;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyNext.Services;

/// <summary>
/// Reads download progress from the configured Radarr and Sonarr queues.
/// </summary>
public class ArrQueueService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ArrQueueService> _logger;

    /// <summary>Initializes a new instance of the <see cref="ArrQueueService"/> class.</summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="cache">A short-lived cache shared by open popups.</param>
    /// <param name="logger">The service logger.</param>
    public ArrQueueService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<ArrQueueService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>Gets progress for a movie or season.</summary>
    /// <param name="item">The discovery title.</param>
    /// <param name="season">The season to track for a series.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The visible queue status, or null when the provider is unconfigured.</returns>
    public async Task<QueueProgress?> GetProgressAsync(ContentItem item, int season, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        var movie = item.Type == ContentType.Movie;
        var url = movie ? config?.RadarrUrl : config?.SonarrUrl;
        var key = movie ? config?.RadarrApiKey : config?.SonarrApiKey;
        var providerId = movie ? item.TmdbId : item.TvdbId;
        if (providerId is not > 0 || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key)
            || !Uri.TryCreate(url.TrimEnd('/') + "/", UriKind.Absolute, out var address)
            || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        var source = movie ? "Radarr" : "Sonarr";
        return await _cache.GetOrCreateAsync(
            ("JellyNext.ArrQueue", source, address, providerId.Value, season),
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(6));
                try
                {
                    using var client = _httpClientFactory.CreateClient(NamedClient.Default);
                    client.BaseAddress = address;
                    client.DefaultRequestHeaders.Add("X-Api-Key", key);
                    return await ReadQueueAsync(client, source, movie, providerId.Value, season, timeout.Token);
                }
                catch (Exception exception) when (exception is HttpRequestException or JsonException
                    || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
                {
                    _logger.LogDebug(exception, "Could not read {Source} download progress", source);
                    return new QueueProgress(source, "unavailable");
                }
            });
    }

    private static async Task<QueueProgress> ReadQueueAsync(
        HttpClient client,
        string source,
        bool movie,
        int providerId,
        int season,
        CancellationToken cancellationToken)
    {
        var id = movie
            ? (await client.GetFromJsonAsync<RadarrMovie[]>($"api/v3/movie?tmdbId={providerId}", cancellationToken))
                ?.FirstOrDefault(result => result.TmdbId == providerId)?.Id
            : (await client.GetFromJsonAsync<SonarrSeries[]>($"api/v3/series?tvdbId={providerId}", cancellationToken))
                ?.FirstOrDefault(result => result.TvdbId == providerId)?.Id;
        if (id is not > 0)
        {
            return new QueueProgress(source, "waiting");
        }

        var path = movie ? $"api/v3/queue/details?movieId={id}" : $"api/v3/queue/details?seriesId={id}&includeEpisode=true";
        var records = await client.GetFromJsonAsync<QueueRecord[]>(path, cancellationToken) ?? Array.Empty<QueueRecord>();
        var matches = records.Where(record => movie
            ? record.MovieId == id
            : record.SeriesId == id && (record.Episode?.SeasonNumber ?? record.SeasonNumber) == season).ToArray();
        if (matches.Length == 0)
        {
            return new QueueProgress(source, "waiting");
        }

        // A Sonarr season pack can repeat the same download for multiple episodes.
        var downloads = matches.GroupBy(
                record => string.IsNullOrWhiteSpace(record.DownloadId) ? $"queue:{record.Id}" : $"download:{record.DownloadId}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.FirstOrDefault(record => record.Size > 0 && record.SizeLeft.HasValue) ?? group.First())
            .ToArray();
        int? percent = null;
        if (downloads.All(record => record.Size > 0 && record.SizeLeft.HasValue))
        {
            var total = downloads.Sum(record => record.Size!.Value);
            var remaining = downloads.Sum(record => Math.Clamp(record.SizeLeft!.Value, 0, record.Size!.Value));
            percent = remaining == 0 ? 100 : Math.Min(99, (int)Math.Floor(100 * (total - remaining) / total));
        }

        var state = matches.Any(record => record.IsFailure())
            ? "warning"
            : matches.All(record => record.IsImporting()) || percent == 100
                ? "importing"
                : matches.All(record => string.Equals(record.Status, "paused", StringComparison.OrdinalIgnoreCase))
                    ? "paused"
                    : matches.Any(record => record.IsDownloading()) || percent is > 0
                        ? "downloading"
                        : "queued";

        return new QueueProgress(source, state, percent);
    }

    /// <summary>Progress reported by the download provider.</summary>
    /// <param name="Source">Radarr or Sonarr.</param>
    /// <param name="State">The current queue state.</param>
    /// <param name="Percent">Percentage of reported queue bytes transferred.</param>
    public sealed record QueueProgress(string Source, string State, int? Percent = null);

    private sealed class QueueRecord
    {
        public int Id { get; set; }

        public int? MovieId { get; set; }

        public int? SeriesId { get; set; }

        public int? SeasonNumber { get; set; }

        public QueueEpisode? Episode { get; set; }

        public string? DownloadId { get; set; }

        public double? Size { get; set; }

        public double? SizeLeft { get; set; }

        public string? Status { get; set; }

        public string? TrackedDownloadStatus { get; set; }

        public string? TrackedDownloadState { get; set; }

        public bool IsFailure()
            => TrackedDownloadStatus?.ToLowerInvariant() is "warning" or "error"
                || TrackedDownloadState?.ToLowerInvariant() is "failed" or "failedpending" or "importfailed"
                || Status?.ToLowerInvariant() is "failed" or "warning";

        public bool IsImporting()
            => TrackedDownloadState?.ToLowerInvariant() is "importpending" or "importing" or "imported"
                || Status?.ToLowerInvariant() is "completed";

        public bool IsDownloading()
            => Status?.ToLowerInvariant() is "downloading";
    }

    private sealed class QueueEpisode
    {
        public int? SeasonNumber { get; set; }
    }
}
