using System.Text.Json;
using System.Text.Json.Serialization;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>
/// Well-known tag source ids and shared defaults.
/// </summary>
public static class TagSources
{
    public const string ITunes = "itunes";
    public const string MusicBrainz = "musicbrainz";
    public const string Discogs = "discogs";
    public const string Deezer = "deezer";

    /// <summary>Fallback chain order when a track is not found in Apple.</summary>
    public static readonly string[] DefaultOrder = [MusicBrainz, Discogs, Deezer];
}
