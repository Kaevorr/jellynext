using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyNext.Models.Common;
using Jellyfin.Plugin.JellyNext.Models.Radarr;
using Microsoft.Extensions.Caching.Memory;

namespace Jellyfin.Plugin.JellyNext.Services;

/// <summary>Shares catalog metadata between title details, requests, and artwork lookups.</summary>
public sealed class MovieCatalogMetadata : IDisposable
{
    private readonly TraktApi _trakt;
    private readonly RadarrService _radarr;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 4096 });
    private readonly ConcurrencyLimiter _concurrency = new(new ConcurrencyLimiterOptions
    {
        PermitLimit = 4,
        QueueLimit = 24
    });

    private readonly TokenBucketRateLimiter _budget = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = 40,
        TokensPerPeriod = 1,
        ReplenishmentPeriod = TimeSpan.FromSeconds(3),
        AutoReplenishment = true
    });

    /// <summary>Initializes a new instance of the <see cref="MovieCatalogMetadata"/> class.</summary>
    /// <param name="trakt">The Trakt metadata provider.</param>
    /// <param name="radarr">The optional Radarr fallback.</param>
    public MovieCatalogMetadata(TraktApi trakt, RadarrService radarr)
    {
        _trakt = trakt;
        _radarr = radarr;
    }

    internal Task<ContentItem?> GetTraktAsync(string id, CancellationToken cancellationToken)
        => _cache.GetOrCreateAsync(("trakt", id), async entry =>
        {
            entry.Size = 1;
            var result = await _trakt.GetCatalogMovieByImdbAsync(id, cancellationToken);
            entry.AbsoluteExpirationRelativeToNow = result is null ? TimeSpan.FromHours(1) : TimeSpan.FromMinutes(10);
            return result;
        });

    internal Task<RadarrMovie?> GetRadarrAsync(string id, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (string.IsNullOrWhiteSpace(config?.RadarrUrl) || string.IsNullOrWhiteSpace(config.RadarrApiKey))
        {
            return Task.FromResult<RadarrMovie?>(null);
        }

        return _cache.GetOrCreateAsync(("radarr", id, config.RadarrUrl), async entry =>
        {
            entry.Size = 1;
            var result = await _radarr.LookupMovieByImdbAsync(id, cancellationToken);
            entry.AbsoluteExpirationRelativeToNow = result is null ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(10);
            return result;
        });
    }

    internal async Task<(bool Ready, string? Url)> GetPosterAsync(string id, CancellationToken cancellationToken)
    {
        var key = ("poster", id, Plugin.Instance?.Configuration.RadarrUrl);
        if (_cache.TryGetValue<string?>(key, out var url))
        {
            return (true, url);
        }

        using var slot = await _concurrency.AcquireAsync(cancellationToken: cancellationToken);
        if (_cache.TryGetValue(key, out url))
        {
            return (true, url);
        }

        if (!slot.IsAcquired)
        {
            return (false, null);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        if (_cache.TryGetValue<ContentItem?>(("trakt", id), out var metadata))
        {
            url = metadata?.PosterUrl;
            if (url is not null)
            {
                return (true, url);
            }
        }

        using var budget = _budget.AttemptAcquire();
        if (!budget.IsAcquired)
        {
            return (false, null);
        }

        Exception? failure = null;
        try
        {
            url = (await GetTraktAsync(id, timeout.Token))?.PosterUrl;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            failure = exception;
        }

        url ??= RadarrPoster(await GetRadarrAsync(id, timeout.Token));
        if (url is null && failure is not null)
        {
            return (false, null);
        }

        _cache.Set(key, url, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = url is null ? TimeSpan.FromMinutes(30) : TimeSpan.FromHours(12)
        });
        return (true, url);
    }

    internal static string? RadarrPoster(RadarrMovie? movie)
        => movie?.Images?.Where(image => string.Equals(image.CoverType, "poster", StringComparison.OrdinalIgnoreCase))
            .Select(image => image.RemoteUrl)
            .FirstOrDefault(remoteUrl => Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps && uri.Host == "image.tmdb.org" && uri.UserInfo.Length == 0);

    /// <inheritdoc />
    public void Dispose()
    {
        _budget.Dispose();
        _concurrency.Dispose();
        _cache.Dispose();
    }
}
