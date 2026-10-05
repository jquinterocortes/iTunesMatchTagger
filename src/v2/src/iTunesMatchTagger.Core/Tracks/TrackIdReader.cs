namespace iTunesMatchTagger.Core.Tracks;

/// <summary>
/// Reads the iTunes Match catalog ID that Apple embeds in the header of
/// matched audio files: the ASCII marker "song" followed by a big-endian
/// int32 track ID, within the first kilobyte of the file.
/// Ported from upstream <c>Track.GetTrackId()</c>.
/// </summary>
public static class TrackIdReader
{
    public const int HeaderSize = 1024;

    private static readonly byte[] Marker = "song"u8.ToArray();

    /// <summary>
    /// Returns the embedded track ID, or 0 when the file has no readable ID
    /// (unmatched files, unreadable files, corrupt headers).
    /// </summary>
    public static long TryReadTrackId(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return 0;
        }

        var buffer = new byte[HeaderSize];
        int length;
        try
        {
            using var stream = File.OpenRead(filePath);
            length = ReadUpTo(stream, buffer);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }

        return ReadFromBuffer(buffer.AsSpan(0, length));
    }

    /// <summary>Parsing core, exposed for tests with synthetic buffers.</summary>
    public static long ReadFromBuffer(ReadOnlySpan<byte> buffer)
    {
        int markerIndex = buffer.IndexOf(Marker);
        if (markerIndex < 0 || markerIndex + Marker.Length + 4 > buffer.Length)
        {
            return 0;
        }

        // Upstream read the same four bytes and parsed them as a hex string,
        // i.e. big-endian. IDs above int.MaxValue end up negative and are
        // treated as invalid (TryReadTrackId > 0), exactly like upstream.
        int id = (buffer[markerIndex + 4] << 24)
               | (buffer[markerIndex + 5] << 16)
               | (buffer[markerIndex + 6] << 8)
               | buffer[markerIndex + 7];
        return id;
    }

    private static int ReadUpTo(Stream stream, byte[] buffer)
    {
        int total = 0;
        int read;
        while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
        {
            total += read;
        }

        return total;
    }
}
