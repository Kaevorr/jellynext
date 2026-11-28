using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Jellyfin.Plugin.JellyNext.Services;

internal sealed class MovieCatalogSnapshot
{
    private const string Header = "tconst\ttitleType\tprimaryTitle\toriginalTitle\tisAdult\tstartYear\tendYear\truntimeMinutes\tgenres";
    private const CompareOptions TitleComparison = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
    private static readonly CompareInfo TitleComparer = CultureInfo.InvariantCulture.CompareInfo;

    private readonly Dictionary<string, MovieCatalogEntry> _byId;
    private readonly MovieCatalogEntry[] _all;
    private readonly Dictionary<string, MovieCatalogEntry[]> _byGenre;
    private readonly string[] _genres;

    private MovieCatalogSnapshot(Dictionary<string, MovieCatalogEntry> byId)
    {
        _byId = byId;
        _all = byId.Values
            .OrderBy(movie => movie.Title, Comparer<string>.Create((left, right) => TitleComparer.Compare(left, right, TitleComparison)))
            .ThenBy(movie => movie.Year)
            .ThenBy(movie => movie.Id, StringComparer.Ordinal)
            .ToArray();
        _byGenre = _all.SelectMany(movie => movie.Genres.Select(genre => (Genre: genre, Movie: movie)))
            .GroupBy(entry => entry.Genre, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Movie).ToArray(), StringComparer.OrdinalIgnoreCase);
        _genres = _byGenre.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public MovieCatalogEntry? Find(string id) => _byId.GetValueOrDefault(id);

    public IEnumerable<(string Genre, MovieCatalogEntry[] Items)> Previews(string? query, int? year, int size)
    {
        foreach (var genre in _genres)
        {
            var items = Filter(_byGenre[genre], query, year).Take(size).ToArray();
            if (items.Length > 0)
            {
                yield return (genre, items);
            }
        }
    }

    public (MovieCatalogEntry[] Items, int PageCount) Browse(string? genre, string? query, int? year, int page, int size)
    {
        var source = genre is null ? _all : _byGenre.GetValueOrDefault(genre) ?? Array.Empty<MovieCatalogEntry>();
        var offset = ((long)page - 1) * size;
        if (string.IsNullOrEmpty(query) && year is null)
        {
            return (offset < source.Length ? source.Skip((int)offset).Take(size).ToArray() : Array.Empty<MovieCatalogEntry>(),
                source.Length == 0 ? 0 : ((source.Length - 1) / size) + 1);
        }

        var items = new List<MovieCatalogEntry>(size);
        var count = 0;
        foreach (var movie in Filter(source, query, year))
        {
            if (count >= offset && items.Count < size)
            {
                items.Add(movie);
            }

            count++;
        }

        return (items.ToArray(), count == 0 ? 0 : ((count - 1) / size) + 1);
    }

    public static MovieCatalogSnapshot Read(string path)
    {
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        if (reader.ReadLine() != Header)
        {
            throw new InvalidDataException("The IMDb title basics header is invalid.");
        }

        var entries = new Dictionary<string, MovieCatalogEntry>(StringComparer.Ordinal);
        var genreNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            if (!line.AsSpan(line.IndexOf('\t') + 1).StartsWith("movie\t", StringComparison.Ordinal))
            {
                continue;
            }

            var fields = line.Split('\t');
            if (fields.Length != 9 || fields[4] != "0" || string.IsNullOrWhiteSpace(fields[2])
                || fields[2] == @"\N" || !IsImdbId(fields[0]) || entries.ContainsKey(fields[0]))
            {
                continue;
            }

            var labels = fields[8] == @"\N" ? Array.Empty<string>()
                : fields[8].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (labels.Contains("Adult", StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var year = int.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : (int?)null;
            var genres = labels.Length == 0
                ? new[] { "Uncategorized" }
                : labels
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(genre =>
                    {
                        genreNames.TryAdd(genre, genre);
                        return genreNames[genre];
                    }).ToArray();
            entries.Add(fields[0], new MovieCatalogEntry(fields[0], fields[2], fields[3] == @"\N" || fields[3] == fields[2] ? null : fields[3], year, genres));
        }

        if (entries.Count == 0)
        {
            throw new InvalidDataException("The IMDb title basics file contains no movie entries.");
        }

        return new MovieCatalogSnapshot(entries);
    }

    private static IEnumerable<MovieCatalogEntry> Filter(IEnumerable<MovieCatalogEntry> source, string? query, int? year)
        => source.Where(movie => (!year.HasValue || movie.Year == year)
            && (string.IsNullOrEmpty(query) || TitleComparer.IndexOf(movie.Title, query, TitleComparison) >= 0
                || (movie.OriginalTitle is { } original && TitleComparer.IndexOf(original, query, TitleComparison) >= 0)));

    private static bool IsImdbId(string value)
        => value.Length is >= 9 and <= 14 && value.StartsWith("tt", StringComparison.Ordinal)
            && value.AsSpan(2).IndexOfAnyExceptInRange('0', '9') < 0;
}
