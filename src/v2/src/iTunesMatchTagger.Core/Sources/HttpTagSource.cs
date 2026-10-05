using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>
/// Base for HTTP tag sources: shared HttpClient management, User-Agent
/// and a JSON GET helper. Sources own their client only when they created
/// it (the embedder may pass its own in tests or share one socket handler).
/// </summary>
public abstract class HttpTagSource : ITagSource
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    protected HttpTagSource(HttpClient? httpClient)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            var agent = UserAgent;
            if (agent is not null)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(agent.ToString());
            }
        }
    }

    /// <summary>Agent passed to the API (MusicBrainz requires a descriptive one).</summary>
    protected abstract TagSourceUserAgent? UserAgent { get; }

    public abstract string Id { get; }

    public virtual bool RequiresCredentials => false;

    public abstract string Description { get; }

    public abstract Task<IReadOnlyList<TagCandidate>> SearchAsync(TagQuery query, CancellationToken cancellationToken = default);

    public virtual Task<byte[]> DownloadArtworkAsync(string artworkUrl, int size = 600, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artworkUrl);
        return _httpClient.GetByteArrayAsync(artworkUrl, cancellationToken);
    }

    /// <summary>GET JSON (case-insensitive names) and deserialize; HttpClient-level failure surfaces as HttpRequestException.</summary>
    protected async Task<T> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
        => await GetJsonAsync<T>(url, null, cancellationToken).ConfigureAwait(false);

    /// <summary>GET JSON (case-insensitive names) with a per-request configuration hook (e.g. auth headers). The User-Agent is already on the client's default headers.</summary>
    protected async Task<T> GetJsonAsync<T>(string url, Action<HttpRequestMessage>? configureRequest, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        configureRequest?.Invoke(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync<T>(SourceJson.Options, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TagSourceException("Empty response body.");
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}

/// <summary>Serializer options shared by the tag source clients.</summary>
public static class SourceJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}
