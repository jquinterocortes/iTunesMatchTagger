using iTunesMatchTagger.Core.Sources;

namespace iTunesMatchTagger.Core.Tests.Sources;

public sealed class LyricsClientTests
{
    private const string ExactRecordJson = """
        {
          "id": 10,
          "trackName": "Numb",
          "artistName": "Linkin Park",
          "albumName": "Meteora",
          "duration": 216,
          "instrumental": false,
          "plainLyrics": "plain text lyrics",
          "syncedLyrics": "[00:10.20]plain text lyrics"
        }
        """;

    [Fact]
    public async Task LookupAsync_ExactHit_PrefersSynced()
    {
        using var client = new LyricsClient(new HttpClient(new FakeHttpHandler(_ => FakeHttpHandler.Json(ExactRecordJson))));

        var result = await client.LookupAsync("Linkin Park", "Numb", "Meteora", 216000);

        Assert.NotNull(result);
        Assert.Equal("[00:10.20]plain text lyrics", result.Synced);
        Assert.Equal("plain text lyrics", result.Plain);
        Assert.True(result.Synced == result.Best);
        Assert.False(result.IsEmpty);
    }

    [Fact]
    public async Task LookupAsync_ExactMiss_FallsBackToSearchAndPicksDuration()
    {
        HttpRequestMessage? searchSeen = null;
        const string searchJson = """
            [
              { "id": 1, "trackName": "Numb", "artistName": "Elsewhere", "duration": 900, "plainLyrics": "too long" },
              { "id": 2, "trackName": "Numb", "artistName": "Elsewhere", "duration": 217, "plainLyrics": "the plain one" }
            ]
            """;
        using var client = new LyricsClient(new HttpClient(new FakeHttpHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/get?", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound); // LRCLib: no exact match
            }

            searchSeen = request;
            return FakeHttpHandler.Json(searchJson);
        })));

        var result = await client.LookupAsync("Linkin Park", "Numb", "Meteora", 216000);

        Assert.NotNull(searchSeen);
        Assert.Contains("/search?q=", searchSeen.RequestUri!.ToString());

        Assert.NotNull(result);
        Assert.Equal("the plain one", result.Plain);
        Assert.Null(result.Synced);
        Assert.Equal("the plain one", result.Best);
    }

    [Fact]
    public async Task LookupAsync_WithoutDuration_GoesStraightToSearch()
    {
        var gotSearch = false;
        const string searchJson = """[{ "id": 3, "trackName": "X", "artistName": "Y", "duration": 100, "syncedLyrics": "[00:01.00]hi" }]""";
        using var client = new LyricsClient(new HttpClient(new FakeHttpHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            Assert.DoesNotContain("/get?", url);
            gotSearch = url.Contains("/search?", StringComparison.Ordinal);
            return FakeHttpHandler.Json(searchJson);
        })));

        var result = await client.LookupAsync("Y", "X", null, null);

        Assert.True(gotSearch);
        Assert.Equal("[00:01.00]hi", result!.Synced);
    }

    [Fact]
    public async Task LookupAsync_Instrumental_ReturnsFlagWithoutText()
    {
        const string json = """{"id": 5, "trackName": "Intro", "artistName": "Band", "duration": 60, "instrumental": true, "plainLyrics": null, "syncedLyrics": null}""";
        using var client = new LyricsClient(new HttpClient(new FakeHttpHandler(_ => FakeHttpHandler.Json(json))));

        var result = await client.LookupAsync("Band", "Intro", null, 60000);

        Assert.NotNull(result);
        Assert.True(result.Instrumental);
        Assert.True(result.IsEmpty);
        Assert.Null(result.Best);
    }

    [Fact]
    public async Task LookupAsync_WithoutTitle_ReturnsNull()
    {
        using var client = new LyricsClient(new HttpClient(new FakeHttpHandler(_ => throw new InvalidOperationException("no request expected"))));

        Assert.Null(await client.LookupAsync("Linkin Park", null, null, null));
    }
}
