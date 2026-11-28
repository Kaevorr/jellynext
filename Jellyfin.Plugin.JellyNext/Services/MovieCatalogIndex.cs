using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.JellyNext.Services;

/// <summary>
/// Loads one immutable movie catalog snapshot per server run.
/// </summary>
public sealed class MovieCatalogIndex
{
    internal const string FileName = "title.basics.tsv.gz";

    private readonly object _gate = new();
    private Task<MovieCatalogSnapshot>? _load;
    private DateTime _lastAttemptUtc;

    internal Task<MovieCatalogSnapshot> OpenAsync(string path, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_load is null || (_load.IsFaulted && DateTime.UtcNow - _lastAttemptUtc >= TimeSpan.FromSeconds(5)))
            {
                _lastAttemptUtc = DateTime.UtcNow;
                _load = Task.Run(() => MovieCatalogSnapshot.Read(path));
            }

            return _load.WaitAsync(cancellationToken);
        }
    }
}
