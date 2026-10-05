using System.Text.Json.Serialization;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>Describes a tag source in settings (persisted identically next run).</summary>
public sealed class TagSourceOptions
{
    /// <summary>Well-known source id (<see cref="TagSources"/>).</summary>
    public required string Id { get; set; }

    public bool Enabled { get; set; }

    /// <summary>1-based fallback order (Apple is always consulted first).</summary>
    public int Order { get; set; }

    /// <summary>Discogs personal access token; empty for free sources.</summary>
    public string? Token { get; set; }
}
