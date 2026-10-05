using System.Text.Json.Serialization;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>Discogs search response for the /database/search endpoint.</summary>
public sealed class DiscogsSearchResponse
{
    [JsonPropertyName("pagination")]
    public DiscogsPagination? Pagination { get; set; }

    [JsonPropertyName("results")]
    public List<DiscogsSearchResult> Results { get; set; } = [];
}

public sealed class DiscogsPagination
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pages")]
    public int Pages { get; set; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    [JsonPropertyName("items")]
    public int Items { get; set; }
}

public sealed class DiscogsSearchResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>"release", "master" or "artist".</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>"Artist - Title" composite; the part before " - " is the artist.</summary>
    [JsonPropertyName("artist")]
    public string? Artist { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("year")]
    public string? Year { get; set; }

    [JsonPropertyName("format")]
    public List<string>? Format { get; set; }

    [JsonPropertyName("label")]
    public List<string>? Label { get; set; }

    [JsonPropertyName("genre")]
    public List<string>? Genre { get; set; }

    [JsonPropertyName("style")]
    public List<string>? Style { get; set; }

    [JsonPropertyName("catno")]
    public string? CatalogNumber { get; set; }

    [JsonPropertyName("barcode")]
    public List<string>? Barcode { get; set; }

    [JsonPropertyName("master_id")]
    public long? MasterId { get; set; }

    [JsonPropertyName("master_url")]
    public string? MasterUrl { get; set; }

    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("resource_url")]
    public string? ResourceUrl { get; set; }

    [JsonPropertyName("cover_image")]
    public string? CoverImage { get; set; }

    [JsonPropertyName("thumb")]
    public string? Thumb { get; set; }

    [JsonPropertyName("community")]
    public DiscogsCommunity? Community { get; set; }
}

public sealed class DiscogsCommunity
{
    [JsonPropertyName("have")]
    public int Have { get; set; }

    [JsonPropertyName("want")]
    public int Want { get; set; }
}
