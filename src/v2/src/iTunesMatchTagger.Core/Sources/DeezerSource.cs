using System.Text.Json.Serialization;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>
/// Tag candidates from Deezer's public REST API (api.deezer.com). Free,
/// no key. Known for coverage of popular music; returns reliable artist,
/// album, duration and a direct cover URL.
/// </summary>
public sealed class DeezerSource : HttpTagSource
{
    public const string BaseUrl = "https://api.deezer.com/";

    public DeezerSource(HttpClient? httpClient = null)
        : base(httpClient)
    {
    }

    protected override TagSourceUserAgent? UserAgent => null;

    public override string Id => TagSources.Deezer;

    public override string Description => "Deezer (free, popular-music coverage)";

    public override async Task<IReadOnlyList<TagCandidate>> SearchAsync(TagQuery query, CancellationToken cancellationToken = default)
    {
        var term = query.ToString();
        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        var url = $"{BaseUrl}search/track?q={Uri.EscapeDataString(term)}&limit=5";
        var response = await GetJsonAsync<DeezerSearchResponse>(url, cancellationToken).ConfigureAwait(false);
        return [.. response.Data.Select(ToCandidate)];
    }

    private static TagCandidate ToCandidate(DeezerTrack track) => new()
    {
        SourceId = TagSources.Deezer,
        Title = track.Title ?? string.Empty,
        Artist = track.Artist?.Name,
        Album = track.Album?.Title,
        Year = ParseYear(track.Album?.ReleaseDate ?? track.ReleaseDate),
        TrackNumber = track.TrackPosition,
        DiscNumber = track.DiskNumber,
        TrackCount = track.Album?.NbTracks,
        Genre = null, // would need a separate /genre/{id} request
        ArtworkUrl = track.Album?.CoverXl ?? track.Album?.CoverBig ?? track.Album?.CoverMedium,
        Details = track.Album?.ReleaseDate is { Length: >= 4 } date ? $"released {date}" : null,
    };

    private static int? ParseYear(string? date)
    {
        if (string.IsNullOrWhiteSpace(date) || date.Length < 4 || !int.TryParse(date.AsSpan(0, 4), out var year))
        {
            return null;
        }

        return year;
    }
}

/// <summary>Deezer search response (their "data" array shape).</summary>
public sealed class DeezerSearchResponse
{
    [JsonPropertyName("data")]
    public List<DeezerTrack> Data { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public sealed class DeezerTrack
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("album")]
    public DeezerAlbum? Album { get; set; }

    [JsonPropertyName("artist")]
    public DeezerArtistRef? Artist { get; set; }

    [JsonPropertyName("track_position")]
    public int? TrackPosition { get; set; }

    [JsonPropertyName("disk_number")]
    public int? DiskNumber { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }
}

public sealed class DeezerAlbum
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("cover")]
    public string? Cover { get; set; }

    [JsonPropertyName("cover_medium")]
    public string? CoverMedium { get; set; }

    [JsonPropertyName("cover_big")]
    public string? CoverBig { get; set; }

    [JsonPropertyName("cover_xl")]
    public string? CoverXl { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("nb_tracks")]
    public int? NbTracks { get; set; }
}

public sealed class DeezerArtistRef
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
