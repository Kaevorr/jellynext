using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyNext.Services;

/// <summary>
/// Loads one immutable movie catalog snapshot per server run.
/// </summary>
public sealed class MovieCatalogIndex
{
    internal const string FileName = "title.basics.tsv.gz";
    private const string SnapshotUrl = "https://datasets.imdbws.com/" + FileName;
    private readonly IHttpClientFactory _clients;
    private readonly ILogger<MovieCatalogIndex> _logger;

    private readonly object _gate = new();
    private Task<MovieCatalogSnapshot>? _load;
    private DateTime _lastAttemptUtc;

    /// <summary>Initializes a new instance of the <see cref="MovieCatalogIndex"/> class.</summary>
    /// <param name="clients">Factory for downloading the official IMDb snapshot.</param>
    /// <param name="logger">The service logger.</param>
    public MovieCatalogIndex(IHttpClientFactory clients, ILogger<MovieCatalogIndex> logger)
    {
        _clients = clients;
        _logger = logger;
    }

    internal Task<MovieCatalogSnapshot> OpenAsync(string path, bool automaticDownload, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_load is null && !automaticDownload && !File.Exists(path))
            {
                return Task.FromException<MovieCatalogSnapshot>(new FileNotFoundException("Movie catalog is not configured.", path));
            }

            if (_load is null || (_load.IsFaulted && DateTime.UtcNow - _lastAttemptUtc >= TimeSpan.FromSeconds(5)))
            {
                _lastAttemptUtc = DateTime.UtcNow;
                _load = Task.Run(() => LoadAsync(path, automaticDownload));
            }

            return _load.WaitAsync(cancellationToken);
        }
    }

    private async Task<MovieCatalogSnapshot> LoadAsync(string path, bool automaticDownload)
    {
        var hasLocalFile = File.Exists(path);
        if (!automaticDownload)
        {
            return MovieCatalogSnapshot.Read(path);
        }

        if (hasLocalFile && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(7))
        {
            try
            {
                return MovieCatalogSnapshot.Read(path);
            }
            catch (InvalidDataException)
            {
                _logger.LogWarning("Cached IMDb snapshot is invalid. Fetching a replacement.");
            }
        }

        var temporary = path + ".download";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var client = _clients.CreateClient("JellyNext.ImdbCatalog");
            using var response = await client.GetAsync(SnapshotUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            await using (var output = File.Create(temporary))
            {
                await response.Content.CopyToAsync(output, timeout.Token);
                if (response.Content.Headers.ContentLength is { } expected && output.Length != expected)
                {
                    throw new InvalidDataException("The IMDb snapshot download was incomplete.");
                }
            }

            var snapshot = MovieCatalogSnapshot.Read(temporary);
            File.Move(temporary, path, overwrite: true);
            return snapshot;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or OperationCanceledException)
        {
            if (!hasLocalFile)
            {
                throw new IOException("Could not download the IMDb snapshot.", exception);
            }

            _logger.LogWarning(exception, "Could not refresh the IMDb snapshot. Using the cached snapshot.");
            return MovieCatalogSnapshot.Read(path);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
