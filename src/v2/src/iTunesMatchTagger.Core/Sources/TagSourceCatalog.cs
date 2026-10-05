using System.Text.Json.Serialization;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>
/// The catalog of known tag sources: display names, source-agnostic ids
/// and their position in the fallback chain. Apple is always first; the
/// rest follow the order the user configured.
/// </summary>
public static class TagSourceCatalog
{
    /// <summary>Ordered list starting with the always-first Apple source.</summary>
    public static readonly IReadOnlyList<TagSourceInfo> All =
    [
        new(TagSources.ITunes, "Apple (iTunes Search API)", RequiresToken: false),
        new(TagSources.MusicBrainz, "MusicBrainz", RequiresToken: false),
        new(TagSources.Discogs, "Discogs", RequiresToken: true),
        new(TagSources.Deezer, "Deezer", RequiresToken: false),
    ];

    public static TagSourceInfo? ById(string id) => All.FirstOrDefault(s => s.Id == id);
}

/// <summary>Static description of one source (id, label, credentials).</summary>
public sealed record TagSourceInfo(string Id, string DisplayName, bool RequiresToken);

/// <summary>User options for one source (JSON-persisted in AppSettings).</summary>
public sealed class TagSourceSettings
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }
}
