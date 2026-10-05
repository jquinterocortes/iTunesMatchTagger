using iTunesMatchTagger.Core.Sources;

namespace iTunesMatchTagger.Core.Tests.Sources;

public sealed class DeezerSourceTests
{
    private const string SearchResponseJson = """
        {
          "data": [
            {
              "id": 1,
              "title": "Numb",
              "duration": 216,
              "track_position": 3,
              "disk_number": 1,
              "artist": { "id": 10, "name": "Linkin Park" },
              "album": {
                "id": 100,
                "title": "Meteora",
                "cover": "https://e-cdns-images.dzcdn.net/images/cover/1/56x56-000000-80-0-0.jpg",
                "cover_medium": "https://e-cdns-images.dzcdn.net/images/cover/1/250x250-000000-80-0-0.jpg",
                "cover_big": "https://e-cdns-images.dzcdn.net/images/cover/1/500x500-000000-80-0-0.jpg",
                "cover_xl": "https://e-cdns-images.dzcdn.net/images/cover/1/1000x1000-000000-80-0-0.jpg",
                "release_date": "2003-03-25",
                "nb_tracks": 13
              }
            }
          ],
          "total": 1
        }
        """;

    [Fact]
    public async Task SearchAsync_Maps_TrackToCandidate()
    {
        using var source = new DeezerSource(new HttpClient(new FakeHttpHandler(_ => FakeHttpHandler.Json(SearchResponseJson))));

        var candidates = await source.SearchAsync(new TagQuery(Artist: "Linkin Park", Title: "Numb"));

        var candidate = Assert.Single(candidates);
        Assert.Equal(TagSources.Deezer, candidate.SourceId);
        Assert.Equal("Numb", candidate.Title);
        Assert.Equal("Linkin Park", candidate.Artist);
        Assert.Equal("Meteora", candidate.Album);
        Assert.Equal(2003, candidate.Year);
        Assert.Equal(3, candidate.TrackNumber);
        Assert.Equal(13, candidate.TrackCount);
        Assert.Equal(1, candidate.DiscNumber);
        Assert.Equal("https://e-cdns-images.dzcdn.net/images/cover/1/1000x1000-000000-80-0-0.jpg", candidate.ArtworkUrl);
    }

    [Fact]
    public async Task SearchAsync_UsesSearchTrackEndpoint()
    {
        HttpRequestMessage? seen = null;
        using var source = new DeezerSource(new HttpClient(new FakeHttpHandler(request =>
        {
            seen = request;
            return FakeHttpHandler.Json("""{"data":[]}""");
        })));

        await source.SearchAsync(new TagQuery(Artist: null, Title: "Numb"));

        Assert.NotNull(seen);
        Assert.Contains("/search/track?q=", seen.RequestUri!.ToString());
    }

    [Fact]
    public async Task SearchAsync_WithNoTerms_ReturnsEmpty()
    {
        using var source = new DeezerSource(new HttpClient(new FakeHttpHandler(_ => throw new InvalidOperationException("should not be called"))));

        var candidates = await source.SearchAsync(new TagQuery(Artist: null, Title: null));

        Assert.Empty(candidates);
    }
}
