using iTunesMatchTagger.Core.Fields;

namespace iTunesMatchTagger.Core.Tracks;

/// <summary>
/// Storage-agnostic view of one track. Implementations wrap either the
/// iTunes COM object model (<see cref="ComTrack"/>) or a plain audio file
/// (<see cref="FileTrack"/>), so the app workflow is identical in both modes.
/// </summary>
public interface ITaggableTrack
{
    /// <summary>File path of the track, or null when not available locally.</summary>
    string? Location { get; }

    /// <summary>iTunes catalog ID parsed from the file header, or 0 when absent.</summary>
    long TrackId { get; }

    /// <summary>
    /// True when the file carries the embedded iTunes Match catalog ID.
    /// </summary>
    bool IsMatched { get; }

    object? ReadField(TrackField field);

    void WriteField(TrackField field, object? value);

    /// <summary>Applies a batch of writes; implementations may optimize by keeping the file open.</summary>
    void WriteFields(IReadOnlyList<KeyValuePair<TrackField, object?>> writes)
    {
        foreach (var write in writes)
        {
            WriteField(write.Key, write.Value);
        }
    }

    /// <summary>Replaces the embedded album artwork with the given image bytes.</summary>
    void WriteArtwork(byte[] imageBytes);

    /// <summary>Track length in milliseconds, when the back end can read it.</summary>
    int? DurationMs => null;

    /// <summary>Replaces the lyrics tag (plain text or synced LRC text).</summary>
    void WriteLyrics(string lyrics);

    /// <summary>Current lyrics text stored in the tag, or null when there is none (or unreadable).</summary>
    string? ReadLyrics() => null;
}
