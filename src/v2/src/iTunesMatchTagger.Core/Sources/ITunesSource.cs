using iTunesMatchTagger.Core.Lookup;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>
/// The Apple/iTunes Search API as a tag source. Reuses
/// <see cref="ITunesSearchClient"/> (lookup + term search + sized artwork)
/// and maps its DTO into source-agnostic <see cref="TagCandidate"/>s.
/// </summary>
public sealed class ITunesSource : ITagSource
{
    private readonly ITunesSearchClient _search;
    private readonly bool _ownsClient;

    public ITunesSource(ITunesSearchClient? search = null)
    {
        _ownsClient = search is null;
        _search = search ?? new ITunesSearchClient();
    }

    public string Id => TagSources.ITunes;

    public bool RequiresCredentials => false;

    public string Description => "Apple / iTunes Search API";

    /// <summary>
    /// Term search over /search. The ID lookup across storefronts stays in
    /// the app's per-track routine because it needs the country list; this
    /// method mirrors that fallback for source-order searches.
    /// Uses US as the storefront because term search is store-independent
    /// for metadata purposes.
    /// </summary>
    public async Task<IReadOnlyList<TagCandidate>> SearchAsync(TagQuery query, CancellationToken cancellationToken = default)
    {
        var term = query.ToString();
        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        var results = await _search.SearchAsync(term, "US", cancellationToken).ConfigureAwait(false);
        return [.. results
            .Where(static r => r.Kind is null or "song")
            .Select(ToCandidate)];
    }

    public async Task<byte[]> DownloadArtworkAsync(string artworkUrl, int size = 600, CancellationToken cancellationToken = default)
    {
        return await _search.DownloadArtworkAsync(artworkUrl, size, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Wraps a direct Apple lookup result (the ID lookup path) as a candidate.</summary>
    public static TagCandidate FromLookupResult(ITunesLookupResult r) => new()
    {
        SourceId = TagSources.ITunes,
        Title = r.TrackName ?? string.Empty,
        Artist = r.ArtistName,
        AlbumArtist = r.AlbumArtist,
        Album = r.CollectionName,
        Year = r.Year,
        Genre = r.PrimaryGenreName,
        TrackNumber = r.TrackNumber,
        TrackCount = r.TrackCount,
        DiscNumber = r.DiscNumber,
        DiscCount = r.DiscCount,
        ArtworkUrl = r.ArtworkUrl100,
        Details = $"Apple catalog ID {r.TrackId}",
    };

    private static TagCandidate ToCandidate(ITunesLookupResult r) => FromLookupResult(r);

    /// <summary>Exposes the mapping so other UI layers can promote a lookup hit into a candidate.</summary>
    public static TagCandidate CandidateFromLookupResult(ITunesLookupResult r) => FromLookupResult(r);

    public void Dispose()
    {
        if (_ownsClient)
        {
            _search.Dispose();
        }
    }
}
