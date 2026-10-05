using iTunesMatchTagger.Core.Sources;

namespace iTunesMatchTagger.Core.Tests.Sources;

public sealed class DiscogsSourceTests
{
    private const string SearchResponseJson = """
        {
          "pagination": { "page": 1, "pages": 1, "per_page": 5, "items": 1 },
          "results": [
            {
              "id": 123,
              "type": "release",
              "title": "Back In Black",
              "artist": "AC/DC",
              "year": "1980",
              "country": "US",
              "format": ["Vinyl", "LP", "Album"],
              "label": ["Albert Productions"],
              "catno": "APL-11149",
              "genre": ["Rock"],
              "style": ["Hard Rock"],
              "thumb": "https://api.discogs.com/image/R-90-123-0000.jpg",
              "cover_image": "https://api.discogs.com/image/R-600-123-0000.jpg"
            }
          ]
        }
        """;

    [Fact]
    public async Task SearchAsync_Maps_ReleaseToCandidate()
    {
        using var source = new DiscogsSource(new HttpClient(new FakeHttpHandler(_ => FakeHttpHandler.Json(SearchResponseJson))))
        {
            Token = "test-token",
        };

        var candidates = await source.SearchAsync(new TagQuery(Artist: "AC/DC", Title: "Back In Black"));

        var candidate = Assert.Single(candidates);
        Assert.Equal(TagSources.Discogs, candidate.SourceId);
        Assert.Equal("Back In Black", candidate.Title);
        Assert.Equal("AC/DC", candidate.Artist);
        Assert.Equal(1980, candidate.Year);
        Assert.Equal("Rock", candidate.Genre);
        Assert.Equal("https://api.discogs.com/image/R-600-123-0000.jpg", candidate.ArtworkUrl);
        Assert.Contains("Vinyl", candidate.Details);
        Assert.Contains("APL-11149", candidate.Details);
    }

    [Fact]
    public async Task SearchAsync_UsesDatabaseSearchEndpointAndTypeRelease()
    {
        HttpRequestMessage? seen = null;
        using var source = new DiscogsSource(new HttpClient(new FakeHttpHandler(request =>
        {
            seen = request;
            return FakeHttpHandler.Json("""{"results":[]}""");
        })))
        {
            Token = "abc",
        };

        await source.SearchAsync(new TagQuery(Artist: null, Title: "Back In Black"));

        Assert.NotNull(seen);
        var url = seen.RequestUri!.ToString();
        Assert.Contains("/database/search?", url);
        Assert.Contains("type=release", url);
        Assert.Contains("token=abc", url);
    }

    [Fact]
    public async Task SearchAsync_WithoutToken_ThrowsAuthException()
    {
        using var source = new DiscogsSource(new HttpClient(new FakeHttpHandler(_ => FakeHttpHandler.Json("{}"))));

        var ex = await Assert.ThrowsAsync<TagSourceAuthException>(
            () => source.SearchAsync(new TagQuery(Artist: null, Title: "Back In Black")));

        Assert.Contains("Discogs token", ex.Message);
    }

    [Fact]
    public void RequiresCredentials_True()
    {
        using var source = new DiscogsSource();
        Assert.True(source.RequiresCredentials);
    }
}
