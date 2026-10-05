using System.Net;
using System.Net.Http.Headers;
using System.Text;
using iTunesMatchTagger.Core.Sources;

namespace iTunesMatchTagger.Core.Tests.Sources;

/// <summary>Fakes the wire: canned JSON per URL. Shared by the source tests.</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public List<string> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri?.ToString() ?? string.Empty);
        return Task.FromResult(_respond(request));
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, new MediaTypeHeaderValue("application/json")) };
}

/// <summary>The MusicBrainz /recording search call.</summary>
public sealed class MusicBrainzSourceTests
{
    private const string SearchResponseJson = """
        {
          "created": "2026-01-01T00:00:00",
          "count": 1,
          "offset": 0,
          "recordings": [
            {
              "id": "rec-1",
              "score": 100,
              "title": "Back In Black",
              "length": 255000,
              "artist-credit": [
                { "name": "AC/DC", "joinphrase": "" }
              ],
              "releases": [
                {
                  "id": "rel-1",
                  "title": "Back In Black",
                  "date": "1980-07-25",
                  "release-group": { "id": "rg-1", "primary-type": "Album" }
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task SearchAsync_Maps_RecordingToCandidate()
    {
        using var source = new MusicBrainzSource(new HttpClient(new FakeHttpHandler(_ => FakeHttpHandler.Json(SearchResponseJson))));

        var candidates = await source.SearchAsync(new TagQuery(Artist: "AC/DC", Title: "Back In Black"));

        var candidate = Assert.Single(candidates);
        Assert.Equal(TagSources.MusicBrainz, candidate.SourceId);
        Assert.Equal("Back In Black", candidate.Title);
        Assert.Equal("AC/DC", candidate.Artist);
        Assert.Equal("Back In Black", candidate.Album);
        Assert.Equal(1980, candidate.Year);
        Assert.Equal("https://coverartarchive.org/release/rel-1/front-250", candidate.ArtworkUrl);
    }

    [Fact]
    public async Task SearchAsync_BuildsLuceneQueryAndUsesJsonFormat()
    {
        HttpRequestMessage? seen = null;
        using var source = new MusicBrainzSource(new HttpClient(new FakeHttpHandler(request =>
        {
            seen = request;
            return FakeHttpHandler.Json("""{"recordings":[]}""");
        })));

        await source.SearchAsync(new TagQuery(Artist: "AC/DC", Title: "Back In Black"));

        Assert.NotNull(seen);
        Assert.Equal("/ws/2/recording", seen.RequestUri!.AbsolutePath);
        Assert.Contains("fmt=json", seen.RequestUri.Query, StringComparison.Ordinal);

        // The lucene query travels in the "query" parameter; Uri may
        // re-escape parts, so compare decoded.
        var queryValue = seen.RequestUri.Query
            .TrimStart('?')
            .Split('&')
            .Select(static p => p.Split('=', 2))
            .First(static p => p[0] == "query")[1];
        Assert.Equal("recording:\"Back In Black\" AND artist:\"AC/DC\"", Uri.UnescapeDataString(queryValue));
    }

    [Fact]
    public async Task SearchAsync_WithoutTitle_Throws()
    {
        using var source = new MusicBrainzSource(new HttpClient(new FakeHttpHandler(_ => FakeHttpHandler.Json("{}"))));

        await Assert.ThrowsAsync<ArgumentNullException>(() => source.SearchAsync(new TagQuery(Artist: "AC/DC", Title: null)));
    }

    [Fact]
    public async Task SearchAsync_FiltersLowScores()
    {
        const string json = """{"recordings":[{"id":"a","score":10,"title":"weak"},{"id":"b","score":99,"title":"strong"}]}""";
        using var source = new MusicBrainzSource(new HttpClient(new FakeHttpHandler(_ => FakeHttpHandler.Json(json))));

        var candidates = await source.SearchAsync(new TagQuery(Artist: null, Title: "anything"));

        Assert.Equal(["strong"], candidates.Select(static c => c.Title).ToArray());
    }
}
