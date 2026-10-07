namespace iTunesMatchTagger.Core.Sources;

/// <summary>
/// A single tag candidate returned by one tag source (Apple, MusicBrainz,
/// Discogs, Deezer, ...), normalized into source-agnostic values. Sources
/// fill only what they know; a missing value stays null and is shown as a
/// placeholder in the UI, never written unless a user-set overwrite exists.
/// </summary>
public sealed class TagCandidate
{
    /// <summary>Id of the source that produced this candidate (see <see cref="TagSources"/>).</summary>
    public required string SourceId { get; init; }

    /// <summary>Track (recording) title.</summary>
    public required string Title { get; init; }

    public string? Artist { get; init; }

    public string? AlbumArtist { get; init; }

    public string? Album { get; init; }

    public int? Year { get; init; }

    public string? Genre { get; init; }

    public int? TrackNumber { get; init; }

    public int? TrackCount { get; init; }

    public int? DiscNumber { get; init; }

    public int? DiscCount { get; init; }

    /// <summary>URL of the album artwork at display size, or null.</summary>
    public string? ArtworkUrl { get; init; }

    /// <summary>Composer, when the source provides it (usually empty).</summary>
    public string? Composer { get; init; }

    /// <summary>Optional one-line extra detail shown in the candidate picker (release format, storefront, ...).</summary>
    public string? Details { get; init; }
}
