using System.Text.Json;
using System.Text.Json.Serialization;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>
/// Lyrics metadata fetched from LRCLib (https://lrclib.net - free, no API
/// key). Synced lyrics come as LRC text ([mm:ss.xx] line stamps) which
/// players that support them render karaoke-style; plain text is the
/// fallback. Apple's own syllable-level timed text is not publicly
/// exposed, so LRCLib's line-level LRC is the best synced form available.
/// </summary>
public sealed record LyricsResult(
    string Artist,
    string Title,
    string? Plain,
    string? Synced,
    bool Instrumental,
    long TrackId)
{
    /// <summary>Synced LRC when available, else the plain text; null when none.</summary>
    public string? Best => Synced ?? Plain;

    public bool IsEmpty => Best is null;
}

/// <summary>
/// LRCLib REST client: an exact /api/get lookup first (it needs the track
/// duration), then a /api/search fallback matched on duration proximity.
/// </summary>
public sealed class LyricsClient : IDisposable
{
    public const string ApiBase = "https://lrclib.net/api/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public LyricsClient(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("iTunesMatchTagger/2.0 (+https://github.com/jquinterocortes/iTunesMatchTagger)");
        }
    }

    /// <summary>
    /// Looks up lyrics for a track. Any parameter may be null; the exact
    /// endpoint is only tried when the duration is known. Returns null when
    /// nothing matched, or a record (possibly Instrumental-only).
    /// </summary>
    public async Task<LyricsResult?> LookupAsync(
        string? artist,
        string? title,
        string? album,
        int? durationMs,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
        {
            return null;
        }

        var titleTrim = title.Trim();
        var artistTrim = artist.Trim();
        double? durationSeconds = durationMs is > 0 ? Math.Round(durationMs.Value / 1000.0) : null;

        if (durationSeconds is { } seconds)
        {
            var getUrl = $"{ApiBase}get?artist_name={Uri.EscapeDataString(artistTrim)}" +
                       $"&track_name={Uri.EscapeDataString(titleTrim)}" +
                       $"&duration={seconds}";
            if (!string.IsNullOrWhiteSpace(album))
            {
                getUrl += $"&album_name={Uri.EscapeDataString(album.Trim())}";
            }

            var exact = await QueryAsync(getUrl, throwOnMissing: false, cancellationToken).ConfigureAwait(false);
            if (exact is not null)
            {
                return exact.ToResult(artistTrim, titleTrim);
            }
        }

        // No exact hit (or no duration): free-text search, closest duration wins.
        var term = string.IsNullOrWhiteSpace(album)
            ? $"{artistTrim} {titleTrim}"
            : $"{artistTrim} {titleTrim} {album.Trim()}";
        var searchUrl = $"{ApiBase}search?q={Uri.EscapeDataString(term)}&limit=5";
        var results = await QueryListAsync(searchUrl, cancellationToken).ConfigureAwait(false);
        return PickBest(results, artistTrim, titleTrim, durationSeconds);
    }

    private static LyricsResult? PickBest(List<LyricsApiRecord> results, string artist, string title, double? durationSeconds)
    {
        if (results.Count == 0)
        {
            return null;
        }

        LyricsApiRecord? best = null;
        if (durationSeconds is { } seconds)
        {
            best = results
                .Where(static r => r.Duration is > 0)
                .Where(static r => r.Synced is not null || r.Plain is not null)
                .OrderBy(r => Math.Abs(r.Duration!.Value - seconds))
                .FirstOrDefault();
        }

        best ??= results.FirstOrDefault(static r => r.Synced is not null)
                 ?? results.FirstOrDefault(static r => r.Plain is not null);
        return best?.ToResult(artist, title);
    }

    private async Task<LyricsApiRecord?> QueryAsync(string url, bool throwOnMissing, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null; // LRCLib's "no exact match" signal
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<LyricsApiRecord>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException) when (!throwOnMissing)
        {
            return null;
        }
    }

    private async Task<List<LyricsApiRecord>> QueryListAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var parsed = await JsonSerializer.DeserializeAsync<List<LyricsApiRecord>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return parsed ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}

/// <summary>LRCLib record shape (api/get single object, api/search array element).</summary>
public sealed class LyricsApiRecord
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("trackName")]
    public string? TrackName { get; set; }

    [JsonPropertyName("artistName")]
    public string? ArtistName { get; set; }

    [JsonPropertyName("albumName")]
    public string? AlbumName { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("instrumental")]
    public bool Instrumental { get; set; }

    [JsonPropertyName("plainLyrics")]
    public string? Plain { get; set; }

    [JsonPropertyName("syncedLyrics")]
    public string? Synced { get; set; }

    public LyricsResult ToResult(string artist, string title) => new(
        Artist: ArtistName ?? artist,
        Title: TrackName ?? title,
        Plain: Plain,
        Synced: Synced,
        Instrumental: Instrumental && Plain is null && Synced is null,
        TrackId: Id);
}
