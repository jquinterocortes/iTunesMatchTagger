using System.Net;
using iTunesMatchTagger.Core.Lookup;

namespace iTunesMatchTagger.Core.Tests;

public class ITunesSearchClientTests
{
    private const string SampleResultJson = """
        {
          "resultCount": 1,
          "results": [
            {
              "wrapperType": "track",
              "kind": "song",
              "artistId": 909253,
              "collectionId": 120954021,
              "trackId": 120954025,
              "artistName": "Jack Johnson",
              "collectionName": "Sing-a-Longs and Lullabies for the Film Curious George",
              "trackName": "Upside Down",
              "trackViewUrl": "https://music.apple.com/us/album/upside-down/120954025",
              "releaseDate": "2006-02-21T08:00:00Z",
              "discCount": 1,
              "discNumber": 1,
              "trackCount": 14,
              "trackNumber": 1,
              "trackTimeMillis": 210743,
              "country": "USA",
              "currency": "USD",
              "primaryGenreName": "Rock"
            }
          ]
        }
        """;

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responder(request));
        }
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task LookupTrackAsync_ParsesAppleSampleJson()
    {
        var handler = new FakeHandler(_ => JsonResponse(SampleResultJson));
        using var client = new ITunesSearchClient(new HttpClient(handler));

        var result = await client.LookupTrackAsync(120954025, "US");

        Assert.NotNull(result);
        Assert.Equal("Upside Down", result.TrackName);
        Assert.Equal("Jack Johnson", result.ArtistName);
        Assert.Equal("Sing-a-Longs and Lullabies for the Film Curious George", result.CollectionName);
        Assert.Equal(120954025L, result.TrackId);
        Assert.Equal(1, result.TrackNumber);
        Assert.Equal(1, result.DiscCount);
        Assert.Equal("Rock", result.PrimaryGenreName);
    }

    [Fact]
    public async Task LookupTrackAsync_BuildsCorrectRequestUrl()
    {
        var handler = new FakeHandler(_ => JsonResponse("""{"resultCount":0,"results":[]}"""));
        using var client = new ITunesSearchClient(new HttpClient(handler));

        await client.LookupTrackAsync(123456, "DE");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://itunes.apple.com/lookup?id=123456&country=DE", handler.LastRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task LookupTrackAsync_ReturnsNullWhenResultCountZero()
    {
        var handler = new FakeHandler(_ => JsonResponse("""{"resultCount":0,"results":[]}"""));
        using var client = new ITunesSearchClient(new HttpClient(handler));

        var result = await client.LookupTrackAsync(1, "US");

        Assert.Null(result);
    }

    [Fact]
    public async Task LookupTrackAsync_TakesFirstOfMultipleResults()
    {
        var json = """
            {
              "resultCount": 2,
              "results": [
                { "trackName": "First", "artistName": "A" },
                { "trackName": "Second", "artistName": "B" }
              ]
            }
            """;
        var handler = new FakeHandler(_ => JsonResponse(json));
        using var client = new ITunesSearchClient(new HttpClient(handler));

        var result = await client.LookupTrackAsync(1, "US");

        Assert.NotNull(result);
        Assert.Equal("First", result.TrackName);
    }

    [Fact]
    public async Task LookupTrackAsync_ThrowsForHttpErrors()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = new ITunesSearchClient(new HttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.LookupTrackAsync(1, "US"));
    }

    [Fact]
    public async Task SearchAsync_EscapesTermAndLimitsResults()
    {
        var handler = new FakeHandler(_ => JsonResponse("""{"resultCount":0,"results":[]}"""));
        using var client = new ITunesSearchClient(new HttpClient(handler));

        await client.SearchAsync("Jack Johnson Upside Down", "GB");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            "https://itunes.apple.com/search?term=Jack%20Johnson%20Upside%20Down&country=GB&limit=5",
            handler.LastRequest.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public void Year_GetterDerivesFromReleaseDate()
    {
        var result = new ITunesLookupResult { ReleaseDate = "1984-09-10T07:00:00Z" };

        Assert.Equal(1984, result.Year);
    }

    [Fact]
    public void Year_SetterRewritesOnlyTheYearPart()
    {
        var result = new ITunesLookupResult { ReleaseDate = "1984-09-10T07:00:00Z" };

        result.Year = 1987;

        Assert.StartsWith("1987-", result.ReleaseDate);
        Assert.EndsWith("-10T07:00:00Z", result.ReleaseDate);
    }

    [Fact]
    public void Year_ReturnsNullWhenReleaseDateMissing()
    {
        Assert.Null(new ITunesLookupResult().Year);
    }

    [Theory]
    [InlineData("https://is1-ssl.mzstatic.com/image/thumb/Music125/v4/ab/cd/ef/source/100x100bb.jpg", "https://is1-ssl.mzstatic.com/image/thumb/Music125/v4/ab/cd/ef/source/600x600bb.jpg")]
    [InlineData("https://is1-ssl.mzstatic.com/image/thumb/Music/xyz/source/100x100bb.png", "https://is1-ssl.mzstatic.com/image/thumb/Music/xyz/source/600x600bb.jpg")]
    [InlineData("https://example.com/some/art.jpg", "https://example.com/some/art.jpg")]
    public void HighResArtworkUrl_RewritesTheSizeSegment(string artworkUrl, string expected)
    {
        Assert.Equal(expected, ITunesSearchClient.HighResArtworkUrl(artworkUrl));
    }

    [Fact]
    public async Task DownloadArtworkAsync_RequestsHighResUrl()
    {
        byte[]? downloaded = null;
        var handler = new FakeHandler(request =>
        {
            downloaded = [0xFF, 0xD8, 0xFF]; // JPEG magic bytes
            Assert.NotNull(request.RequestUri);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(downloaded),
            };
        });
        using var client = new ITunesSearchClient(new HttpClient(handler));

        var bytes = await client.DownloadArtworkAsync("https://is1-ssl.mzstatic.com/image/thumb/Music/a/source/100x100bb.jpg");

        Assert.Equal([0xFF, 0xD8, 0xFF], bytes);
        Assert.Equal("https://is1-ssl.mzstatic.com/image/thumb/Music/a/source/600x600bb.jpg", handler.LastRequest?.RequestUri?.ToString());
    }
}
