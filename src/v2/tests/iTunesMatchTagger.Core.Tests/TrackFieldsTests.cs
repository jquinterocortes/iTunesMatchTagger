using iTunesMatchTagger.Core.Fields;
using iTunesMatchTagger.Core.Lookup;
using TagLib;

namespace iTunesMatchTagger.Core.Tests;

public class TrackFieldsTests
{
    [Fact]
    public void All_LookupMembersAreUnique()
    {
        var members = TrackFields.All.Select(f => f.LookupMember).ToList();

        Assert.Equal(members.Count, members.Distinct().Count());
    }

    [Fact]
    public void Visible_MatchesUpstreamDefaults()
    {
        // upstream InitUpdateOptions(): these six had update = true
        var expectedDefaultOn = new[] { "trackName", "artistName", "AlbumArtist", "collectionName", "year", "primaryGenreName" };

        foreach (var field in TrackFields.All)
        {
            Assert.Equal(expectedDefaultOn.Contains(field.LookupMember), field.UpdateByDefault);
        }

        // upstream "Filename" option had ShowOption = false
        var filename = Assert.Single(TrackFields.All, f => f.LookupMember == "Filename");
        Assert.False(filename.VisibleInOptions);
        Assert.True(TrackFields.Visible.All(f => f.LookupMember != "Filename"));
    }

    [Fact]
    public void GetFromLookup_ReadsEveryMappedValue()
    {
        var lookup = new ITunesLookupResult
        {
            TrackName = "Upside Down",
            ArtistName = "Jack Johnson",
            AlbumArtist = "Jack Johnson",
            CollectionName = "Curious George OST",
            ReleaseDate = "2006-02-21T08:00:00Z",
            PrimaryGenreName = "Rock",
            TrackNumber = 1,
            TrackCount = 14,
            DiscNumber = 1,
            DiscCount = 1,
        };

        Assert.Equal("Upside Down", TrackFields.TrackName.GetFromLookup(lookup));
        Assert.Equal("Jack Johnson", TrackFields.ArtistName.GetFromLookup(lookup));
        Assert.Equal("Jack Johnson", TrackFields.AlbumArtist.GetFromLookup(lookup));
        Assert.Equal("Curious George OST", TrackFields.AlbumName.GetFromLookup(lookup));
        Assert.Equal(2006, TrackFields.Year.GetFromLookup(lookup));
        Assert.Equal("Rock", TrackFields.Genre.GetFromLookup(lookup));
        Assert.Equal(1, TrackFields.TrackNumber.GetFromLookup(lookup));
        Assert.Equal(14, TrackFields.TrackCount.GetFromLookup(lookup));
        Assert.Equal(1, TrackFields.DiscNumber.GetFromLookup(lookup));
        Assert.Equal(1, TrackFields.DiscCount.GetFromLookup(lookup));
        Assert.Null(TrackFields.Filename.GetFromLookup(lookup));
    }

    [Fact]
    public void CoerceWrite_ParsesNumericStringsForComAndFileWrites()
    {
        Assert.Equal(1984, TrackFields.Year.CoerceWrite?.Invoke("1984"));
        Assert.Equal(7, TrackFields.TrackNumber.CoerceWrite?.Invoke("7"));
        Assert.Null(TrackFields.Year.CoerceWrite?.Invoke("not-a-year"));
        Assert.Null(TrackFields.Year.CoerceWrite?.Invoke(null));
    }

    /// <summary>
    /// Minimal TagLib.Tag stub over the abstract surface - MockTag is part
    /// of TagLibSharp's own test suite, not the NuGet package.
    /// </summary>
    private sealed class StubTag : TagLib.Tag
    {
        public override string Title { get; set; } = string.Empty;
        public override string[] Performers { get; set; } = [];
        public override string[] AlbumArtists { get; set; } = [];
        public override string Album { get; set; } = string.Empty;
        public override string Comment { get; set; } = string.Empty;
        public override string[] Genres { get; set; } = [];
        public override uint Year { get; set; }
        public override uint Track { get; set; }
        public override uint TrackCount { get; set; }
        public override uint Disc { get; set; }
        public override uint DiscCount { get; set; }
        public override string Lyrics { get; set; } = string.Empty;
        public override TagTypes TagTypes => TagTypes.Xiph;
        public override void Clear()
        {
        }
    }

    [Fact]
    public void GetFromFileTag_ReadsTagLibTag()
    {
        var tag = new StubTag
        {
            Title = "Upside Down",
            Performers = ["Jack Johnson"],
            Album = "Curious George OST",
            Year = 2006,
            Track = 1,
        };

        Assert.Equal("Upside Down", TrackFields.TrackName.GetFromFileTag?.Invoke(tag));
        Assert.Equal("Jack Johnson", TrackFields.ArtistName.GetFromFileTag?.Invoke(tag));
        Assert.Equal("Curious George OST", TrackFields.AlbumName.GetFromFileTag?.Invoke(tag));
        Assert.Equal(2006, TrackFields.Year.GetFromFileTag?.Invoke(tag));
        Assert.Equal(1, TrackFields.TrackNumber.GetFromFileTag?.Invoke(tag));
    }

    [Fact]
    public void SetOnFileTag_WritesTagLibTag()
    {
        var tag = new StubTag();

        TrackFields.TrackName.SetOnFileTag?.Invoke(tag, "New Title");
        TrackFields.ArtistName.SetOnFileTag?.Invoke(tag, "New Artist");
        TrackFields.Year.SetOnFileTag?.Invoke(tag, "1984");

        Assert.Equal("New Title", tag.Title);
        Assert.Equal("New Artist", tag.Performers.Single());
        Assert.Equal(1984u, tag.Year);
    }
}
