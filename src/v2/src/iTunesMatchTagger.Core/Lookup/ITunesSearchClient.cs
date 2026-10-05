using System.Text.Json;

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
