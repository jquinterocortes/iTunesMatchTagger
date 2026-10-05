using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace iTunesMatchTagger.Core.Sources;

/// <summary>
/// Tag candidates from the Discogs API (/database/search). Requires a
/// personal access token (created at https://www.discogs.com/settings/developers,
/// scope: no scope needed for search); sent as "user_token" query parameter.
/// Strong for releases, pressings, formats and catalog numbers; cover
/// images come as direct URLs (cover_image).
/// </summary>
public sealed class DiscogsSource : HttpTagSource
{
    public const string BaseUrl = "https://api.discogs.com/";

    public DiscogsSource(HttpClient? httpClient = null)
        : base(httpClient)
    {
    }

    protected override TagSourceUserAgent UserAgent { get; } =
        new("iTunesMatchTagger", "2.0", "+https://github.com/jquinterocortes/iTunesMatchTagger");

    public override string Id => TagSources.Discogs;

    public override bool RequiresCredentials => true;

    public override string Description => "Discogs (token required, physical-release data)";

    /// <summary>Personal access token, set at construction from settings.</summary>
    public string? Token { get; set; }

    public override async Task<IReadOnlyList<TagCandidate>> SearchAsync(TagQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            throw new TagSourceAuthException(
                "Discogs token is not configured. Create one at discogs.com/settings/developers and paste it in Options.");
        }

        var term = query.ToString();
        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        // type=release keeps masters out (a master is a group of reissues,
        // not a concrete release with one artwork per pressing).
        var url = $"{BaseUrl}database/search?" +
                  $"q={Uri.EscapeDataString(term)}" +
                  "&type=release" +
                  $"&token={Uri.EscapeDataString(Token)}" +
                  "&per_page=5";
        var response = await GetJsonAsync<DiscogsSearchResponse>(url, cancellationToken).ConfigureAwait(false);
        return [.. response.Results.Select(ToCandidate)];
    }

    private static TagCandidate ToCandidate(DiscogsSearchResult result)
    {
        // Searching tracks matches "Artist - Title"; split for the fields.
        var artist = result.Artist;
        var title = result.Title;
        var separator = title?.IndexOf(" - ", StringComparison.Ordinal) ?? -1;
        if (artist is null && separator > 0)
        {
            artist = title![..separator];
        }

        return new TagCandidate
        {
            SourceId = TagSources.Discogs,
            Title = title ?? string.Empty,
            Artist = artist,
            Album = result.Label is { Count: > 0 } label ? label[0] : null, // Discogs search hits are title-style; the label set fills album
            Year = ParseYear(result.Year),
            Genre = result.Genre is { Count: > 0 } genre ? genre[0] : result.Style is { Count: > 0 } style ? style[0] : null,
            ArtworkUrl = result.CoverImage ?? result.Thumb,
            Details = BuildDetails(result),
        };
    }

    private static string? BuildDetails(DiscogsSearchResult result)
    {
        var parts = new List<string>();
        if (result.Year is { Length: > 0 } year)
        {
            parts.Add(year);
        }

        if (result.Format is { Count: > 0 })
        {
            parts.Add(string.Join(", ", result.Format.Where(static f => !string.IsNullOrWhiteSpace(f))));
        }

        if (result.Label is { Count: > 0 } lbl)
        {
            var label = lbl[0];
            var catno = string.IsNullOrWhiteSpace(result.CatalogNumber) ? string.Empty : $" ({result.CatalogNumber})";
            parts.Add(label + catno);
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static int? ParseYear(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 4 || !int.TryParse(text.AsSpan(0, 4), out var year))
        {
            return null;
        }

        return year;
    }
}
