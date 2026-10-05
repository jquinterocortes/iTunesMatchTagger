using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>
/// Reads tag candidates from MusicBrainz's public web service (/ws/2).
/// Free, no API key, expect ~1 request per second. Release artwork is
/// fetched from the Cover Art Archive (coverartarchive.org).
/// </summary>
public sealed class MusicBrainzSource : HttpTagSource
{
    public const string BaseUrl = "https://musicbrainz.org/ws/2/";

    public MusicBrainzSource(HttpClient? httpClient = null)
        : base(httpClient)
    {
    }

    protected override TagSourceUserAgent UserAgent { get; } =
        new("iTunesMatchTagger", "2.0", "+https://github.com/jquinterocortes/iTunesMatchTagger");

    public override string Id => TagSources.MusicBrainz;

    public override string Description => "MusicBrainz (free, community-maintained)";

    public override async Task<IReadOnlyList<TagCandidate>> SearchAsync(TagQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Title);

        var terms = new StringBuilder();
        terms.Append("recording:\"").Append(query.Title).Append('"');
        if (!string.IsNullOrWhiteSpace(query.Artist))
        {
            terms.Append(" AND artist:\"").Append(query.Artist).Append('"');
        }

        if (!string.IsNullOrWhiteSpace(query.Album))
        {
            terms.Append(" AND release:\"").Append(query.Album).Append('"');
        }

        var url = $"{BaseUrl}recording?query={Uri.EscapeDataString(terms.ToString())}&fmt=json&limit=5";
        var response = await GetJsonAsync<MusicBrainzSearchResponse>(url, cancellationToken).ConfigureAwait(false);
        return [.. response.Recordings
            .Where(static r => r.Score > 20)
            .Select(ToCandidate)];
    }

    private static TagCandidate ToCandidate(MusicBrainzRecording recording)
    {
        var release = recording.Releases?.FirstOrDefault();
        var artists = recording.ArtistCredit?
            .Select(static a => a.Name)
            .Where(static n => !string.IsNullOrWhiteSpace(n))
            .ToArray();

        return new TagCandidate
        {
            SourceId = TagSources.MusicBrainz,
            Title = recording.Title ?? string.Empty,
            Artist = artists?.Length > 0 ? string.Join(", ", artists) : null,
            Album = release?.Title,
            Year = ParseYear(release?.Date),
            Genre = null, // genres would need a separate /recording/{mbid} lookup
            ArtworkUrl = release?.Id is null ? null : $"https://coverartarchive.org/release/{release.Id}/front-250",
            Details = recording.Disambiguation is { Length: > 0 } disambiguation ? disambiguation : null,
        };
    }

    private static int? ParseYear(string? date)
    {
        if (string.IsNullOrWhiteSpace(date) || date.Length < 4 || !int.TryParse(date.AsSpan(0, 4), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
        {
            return null;
        }

        return year;
    }
}

/// <summary>Recording search response (json serializer names match the ws/2 fields).</summary>
public sealed class MusicBrainzSearchResponse
{
    [JsonPropertyName("created")]
    public string? Created { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("recordings")]
    public List<MusicBrainzRecording> Recordings { get; set; } = [];
}

public sealed class MusicBrainzRecording
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("length")]
    public int? Length { get; set; }

    [JsonPropertyName("disambiguation")]
    public string? Disambiguation { get; set; }

    [JsonPropertyName("artist-credit")]
    public List<MusicBrainzArtistCredit>? ArtistCredit { get; set; }

    [JsonPropertyName("releases")]
    public List<MusicBrainzRelease>? Releases { get; set; }

    [JsonPropertyName("isrcs")]
    public List<string>? Isrcs { get; set; }
}

public sealed class MusicBrainzArtistCredit
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("joinphrase")]
    public string? JoinPhrase { get; set; }
}

public sealed class MusicBrainzRelease
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("release-group")]
    public MusicBrainzReleaseGroup? ReleaseGroup { get; set; }
}

public sealed class MusicBrainzReleaseGroup
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("primary-type")]
    public string? PrimaryType { get; set; }
}
