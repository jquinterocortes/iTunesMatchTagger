namespace iTunesMatchTagger.Core.Lookup;

/// <summary>
/// A single result of the iTunes Search API lookup endpoint.
/// JSON properties are matched case-insensitively by <see cref="ITunesSearchClient"/>.
/// Ported from the upstream project (src/itunes-match-tagger, r6).
/// </summary>
public sealed class ITunesLookupResult
{
    public string? WrapperType { get; set; }
    public string? Kind { get; set; }
    public long? ArtistId { get; set; }
    public long? CollectionId { get; set; }
    public long? TrackId { get; set; }
    public string? ArtistName { get; set; }
    public string? AlbumArtist { get; set; }
    public string? CollectionName { get; set; }
    public string? TrackName { get; set; }
    public string? CollectionCensoredName { get; set; }
    public string? TrackCensoredName { get; set; }
    public string? ArtistViewUrl { get; set; }
    public string? CollectionViewUrl { get; set; }
    public string? TrackViewUrl { get; set; }
    public string? PreviewUrl { get; set; }
    public string? ArtworkUrl30 { get; set; }
    public string? ArtworkUrl60 { get; set; }
    public string? ArtworkUrl100 { get; set; }
    public float? CollectionPrice { get; set; }
    public float? TrackPrice { get; set; }
    public string? ReleaseDate { get; set; }
    public string? CollectionExplicitness { get; set; }
    public string? TrackExplicitness { get; set; }
    public int? DiscCount { get; set; }
    public int? DiscNumber { get; set; }
    public int? TrackCount { get; set; }
    public int? TrackNumber { get; set; }
    public long? TrackTimeMillis { get; set; }
    public string? Country { get; set; }
    public string? Currency { get; set; }
    public string? PrimaryGenreName { get; set; }

    /// <summary>
    /// Release year, derived from <see cref="ReleaseDate"/>. Setting it
    /// rewrites the year part of the release date, matching upstream.
    /// </summary>
    public int? Year
    {
        get
        {
            if (string.IsNullOrEmpty(ReleaseDate) || ReleaseDate.Length < 4)
            {
                return null;
            }

            return int.Parse(ReleaseDate[..4]);
        }
        set
        {
            if (string.IsNullOrEmpty(ReleaseDate) || ReleaseDate.Length < 4)
            {
                ReleaseDate = value?.ToString();
            }
            else
            {
                ReleaseDate = value + ReleaseDate[4..];
            }
        }
    }
}

/// <summary>Envelope of the lookup endpoint response.</summary>
public sealed class ITunesLookupResponse
{
    public int ResultCount { get; set; }
    public List<ITunesLookupResult> Results { get; set; } = [];
}
