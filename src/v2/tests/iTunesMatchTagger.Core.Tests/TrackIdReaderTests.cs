using iTunesMatchTagger.Core.Tracks;

namespace iTunesMatchTagger.Core.Tests;

public class TrackIdReaderTests
{
    private const long KnownId = 0x12345678;

    private static byte[] HeaderWithMarkerAt(int markerIndex, int totalLength = TrackIdReader.HeaderSize)
    {
        var buffer = new byte[totalLength];
        for (var i = 0; i < totalLength; i++)
        {
            buffer[i] = 0x20; // padding
        }

        buffer[markerIndex] = (byte)'s';
        buffer[markerIndex + 1] = (byte)'o';
        buffer[markerIndex + 2] = (byte)'n';
        buffer[markerIndex + 3] = (byte)'g';
        buffer[markerIndex + 4] = 0x12;
        buffer[markerIndex + 5] = 0x34;
        buffer[markerIndex + 6] = 0x56;
        buffer[markerIndex + 7] = 0x78;
        return buffer;
    }

    private static string WriteTempFile(byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"imt-test-{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void ReadFromBuffer_ParsesBigEndianIdAfterSongMarker()
    {
        var buffer = HeaderWithMarkerAt(16);

        var id = TrackIdReader.ReadFromBuffer(buffer);

        Assert.Equal(KnownId, id);
    }

    [Fact]
    public void ReadFromBuffer_ReturnsZeroWhenMarkerMissing()
    {
        var buffer = new byte[TrackIdReader.HeaderSize];

        Assert.Equal(0, TrackIdReader.ReadFromBuffer(buffer));
    }

    [Fact]
    public void ReadFromBuffer_ReturnsZeroWhenMarkerNearEndOfBuffer()
    {
        // marker starts at the last possible position that still fits the
        // marker itself, but leaves fewer than 4 bytes for the ID
        var buffer = new byte[TrackIdReader.HeaderSize];
        buffer[^4] = (byte)'s';
        buffer[^3] = (byte)'o';
        buffer[^2] = (byte)'n';
        buffer[^1] = (byte)'g';

        Assert.Equal(0, TrackIdReader.ReadFromBuffer(buffer));
    }

    [Fact]
    public void ReadFromBuffer_ReadsIdThatFillsBufferToTheLastByte()
    {
        // marker at 1016: "song" occupies 1016..1019, ID 1020..1023
        var buffer = HeaderWithMarkerAt(TrackIdReader.HeaderSize - 8);

        Assert.Equal(KnownId, TrackIdReader.ReadFromBuffer(buffer));
    }

    [Fact]
    public void TryReadTrackId_ReadsIdFromRealFile()
    {
        var path = WriteTempFile(HeaderWithMarkerAt(16));
        try
        {
            Assert.Equal(KnownId, TrackIdReader.TryReadTrackId(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryReadTrackId_ReadsIdFromShortFile()
    {
        var path = WriteTempFile(HeaderWithMarkerAt(0, 64));
        try
        {
            Assert.Equal(KnownId, TrackIdReader.TryReadTrackId(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryReadTrackId_ReturnsZeroForFileWithoutMarker()
    {
        var path = WriteTempFile(new byte[512]);
        try
        {
            Assert.Equal(0, TrackIdReader.TryReadTrackId(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryReadTrackId_ReturnsZeroForMissingFile()
    {
        Assert.Equal(0, TrackIdReader.TryReadTrackId(Path.Combine(Path.GetTempPath(), $"imt-missing-{Guid.NewGuid():N}.m4a")));
    }

    [Fact]
    public void TryReadTrackId_ReturnsZeroForNullOrWhitespacePath()
    {
        Assert.Equal(0, TrackIdReader.TryReadTrackId(""));
        Assert.Equal(0, TrackIdReader.TryReadTrackId("   "));
    }
}
