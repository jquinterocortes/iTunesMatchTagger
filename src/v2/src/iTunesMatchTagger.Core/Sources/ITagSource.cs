using iTunesMatchTagger.Core.Lookup;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>
/// One provider of tag candidates. Implementations are stateless to the
/// thread level and acceptable; the embedder decides which sources are
/// enabled and in what fallback order. One source instance per app
/// lifetime is typical.
/// </summary>
public interface ITagSource : IDisposable
{
    /// <summary>Well-known id (<see cref="TagSources"/> constants).</summary>
    string Id { get; }

    /// <summary>Whether <see cref="SearchAsync"/> needs credentials before use.</summary>
    bool RequiresCredentials { get; }

    /// <summary>Human-readable description shown in the sources list.</summary>
    string Description { get; }

    /// <summary>
    /// Free-text search for a track. Must return candidates ordered by
    /// relevance; may return an empty list when nothing matches.
    /// </summary>
    Task<IReadOnlyList<TagCandidate>> SearchAsync(TagQuery query, CancellationToken cancellationToken = default);

    /// <summary>Downloads album artwork bytes at display size (e.g. 300px) or full size.</summary>
    Task<byte[]> DownloadArtworkAsync(string artworkUrl, int size = 600, CancellationToken cancellationToken = default);
}

/// <summary>Search term built from the track's current tags.</summary>
public sealed record TagQuery(string? Artist, string? Title, string? Album = null)
{
    public override string ToString()
    {
        var parts = new[] { Artist, Title, Album }.Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(" ", parts);
    }
}
