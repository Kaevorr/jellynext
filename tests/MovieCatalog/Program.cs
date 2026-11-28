using System.IO.Compression;
using Jellyfin.Plugin.JellyNext.Services;

const string Header = "tconst\ttitleType\tprimaryTitle\toriginalTitle\tisAdult\tstartYear\tendYear\truntimeMinutes\tgenres";
var path = Path.GetTempFileName();
try
{
    using (var output = new StreamWriter(new GZipStream(File.Create(path), CompressionMode.Compress)))
    {
        output.WriteLine(Header);
        Write("tt0000004", "Zebra in Orbit", "Zebra in Orbit", "2020", "Drama");
        Write("tt0000003", "Quiet Planet", "Une planète", "2021", "Drama,Action,Action");
        Write("tt0000002", "Beta", "Beta", "2020", "Drama");
        Write("tt0000001", "Béta", "Béta", "2019", "Drama");
        Write("tt0000005", "Beta", "Beta", "2020", "Drama");
        Write("tt0000003", "Quiet Planet", "Une planète", "2021", "Action");
        Write("tt0000006", "Unclassified", "Unclassified", @"\N", @"\N");
        Write("tt0000007", "TV", "TV", "2015", "Action", "tvSeries");
        Write("tt0000008", "Adult", "Adult", "2015", "Action", adult: 1);

        void Write(string id, string title, string original, string year, string genres, string type = "movie", int adult = 0)
            => output.WriteLine($"{id}\t{type}\t{title}\t{original}\t{adult}\t{year}\t\\N\t90\t{genres}");
    }

    var index = new MovieCatalogIndex();
    var snapshot = await index.OpenAsync(path, CancellationToken.None);
    var genres = snapshot.Previews(null, null, 8).ToArray();
    Expect(genres.Select(row => row.Genre), ["Action", "Drama", "Uncategorized"], "Alphabetical genre order");
    Expect(genres[0].Items.Select(movie => movie.Id), ["tt0000003"], "Repeated IDs and genre labels are deduplicated");
    Assert(genres[1].Items.Any(movie => movie.Id == "tt0000003"), "A movie appears in every assigned genre");
    Assert(snapshot.Find("tt0000007") is null && snapshot.Find("tt0000008") is null, "Only non-adult movies are indexed");

    var (first, pageCount) = snapshot.Browse("Drama", null, null, 1, 2);
    var (second, _) = snapshot.Browse("Drama", null, null, 2, 2);
    var (third, _) = snapshot.Browse("Drama", null, null, 3, 2);
    Assert(pageCount == 3, "Page count includes all matching titles");
    Expect(first.Concat(second).Concat(third).Select(movie => movie.Id),
        ["tt0000001", "tt0000002", "tt0000005", "tt0000003", "tt0000004"], "Title, year and ID order across pages");
    Assert(third.Single().Title == "Zebra in Orbit", "A title outside the preview remains browseable");
    Assert(snapshot.Browse("Drama", null, null, int.MaxValue, 24).Items.Length == 0, "Out-of-range page is empty");
    Expect(snapshot.Browse("action", "PLANETE", 2021, 1, 24).Items.Select(movie => movie.Id),
        ["tt0000003"], "Genre, original title and year filters combine");
    Assert(snapshot.Browse("Action", "quiet", 2020, 1, 24).Items.Length == 0, "A wrong year rejects a title match");
    Assert(snapshot.Browse("Uncategorized", null, null, 1, 24).Items.Single().Year is null, "Undated films stay browseable");
    File.Delete(path);
    Assert(ReferenceEquals(snapshot, await index.OpenAsync(path, CancellationToken.None)), "Paging uses the same immutable snapshot");
    Console.WriteLine("Movie catalog index tests passed.");

    if (args.Length == 1)
    {
        var real = MovieCatalogSnapshot.Read(args[0]);
        var (all, _) = real.Browse(null, null, null, 1, int.MaxValue);
        Console.WriteLine($"Provider snapshot: {all.Length:N0} unique non-adult movies in {real.Previews(null, null, 1).Count()} genre rows.");
    }
}
finally
{
    File.Delete(path);
}

static void Assert(bool condition, string test)
{
    if (!condition) throw new InvalidOperationException(test);
}

static void Expect(IEnumerable<string> actual, IEnumerable<string> expected, string test)
    => Assert(actual.SequenceEqual(expected), test);
