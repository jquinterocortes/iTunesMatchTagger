// Probe: calls the real LRCLib API through LyricsClient with several
// artist/title/album variants to see which combinations fail.
using iTunesMatchTagger.Core.Sources;

using var client = new LyricsClient();

var cases = new (string? artist, string? title, string? album)[]
{
    ("Don Omar", "Dutty Love (feat. Natti Natasha)", "Don Omar Presents MTO2: New Generation"),
    ("Don Omar", "Dutty Love", null),
    ("Don Omar", "Dutty Love (feat. Natti Natasha)", null),
    ("Matt Hunter", "Dicen", null),
};

foreach (var (artist, title, album) in cases)
{
    var r = await client.LookupAsync(artist, title, album, null);
    var summary = r is null
        ? "NULL"
        : $"synced={(r.Synced is not null)}, plain={!string.IsNullOrEmpty(r.Plain)}, id={r.TrackId}, best={(r.Best?.Length ?? 0)} chars";
    Console.WriteLine($"{artist} | {title} | {album ?? "-"} -> {summary}");
}
