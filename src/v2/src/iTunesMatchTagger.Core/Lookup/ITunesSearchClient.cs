using System.Text.Json;
using System.Text.RegularExpressions;

namespace iTunesMatchTagger.Core.Lookup;

/// <summary>
/// Client for Apple's public iTunes Search API. Replaces the upstream
/// <c>WebClient</c> + <c>DataContractJsonSerializer</c> implementation with
/// <see cref="HttpClient"/> + <see cref="JsonSerializer"/> over HTTPS.
/// </summary>
public sealed class ITunesSearchClient : IDisposable
{
    public const string LookupUrl = "https://itunes.apple.com/lookup";
    public const string SearchUrl = "https://itunes.apple.com/search";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public ITunesSearchClient(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("iTunesMatchTagger/2.0 (+https://github.com/jquinterocortes/iTunesMatchTagger)");
    }

    /// <summary>
    /// Looks a track up by its embedded iTunes catalog ID in the given
    /// storefront country. Returns the first result, or null when the
    /// catalog has no match.
    /// </summary>
    public async Task<ITunesLookupResult?> LookupTrackAsync(long trackId, string country, CancellationToken cancellationToken = default)
    {
        var results = await QueryAsync($"{LookupUrl}?id={trackId}&country={country}", cancellationToken).ConfigureAwait(false);
        return results.Count > 0 ? results[0] : null;
    }

    /// <summary>
    /// Free-text search (artist/album/title) in the given storefront, via
    /// the /search endpoint (the /lookup endpoint only accepts ids).
    /// Used by standalone mode and as a fallback when an embedded Track ID
    /// is no longer listed in the catalog.
    /// </summary>
    public async Task<List<ITunesLookupResult>> SearchAsync(string term, string country, CancellationToken cancellationToken = default)
    {
        return await QueryAsync($"{SearchUrl}?term={Uri.EscapeDataString(term)}&country={country}&limit=5", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads album artwork bytes. Apple artwork URLs end with a size
    /// segment like 100x100bb.jpg; it is rewritten to request 600x600.
    /// </summary>
    public async Task<byte[]> DownloadArtworkAsync(string artworkUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artworkUrl);
        return await _httpClient.GetByteArrayAsync(HighResArtworkUrl(artworkUrl), cancellationToken).ConfigureAwait(false);
    }

    private static readonly Regex SizeSegmentRegex = new(@"^\d+x\d+bb\.(jpg|jpeg|png|webp)$", RegexOptions.Compiled);

    /// <summary>Rewrites the artwork URL's size segment to 600x600. Parsing core, exposed for tests.</summary>
    public static string HighResArtworkUrl(string artworkUrl)
    {
        var lastSlash = artworkUrl.LastIndexOf('/');
        if (lastSlash >= 0)
        {
            var lastSegment = artworkUrl[(lastSlash + 1)..];
            if (SizeSegmentRegex.IsMatch(lastSegment))
            {
                return artworkUrl[..(lastSlash + 1)] + "600x600bb.jpg";
            }
        }

        return artworkUrl;
    }

    private async Task<List<ITunesLookupResult>> QueryAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var parsed = await JsonSerializer.DeserializeAsync<ITunesLookupResponse>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return parsed?.Results ?? [];
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
