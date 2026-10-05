using iTunesMatchTagger.Core.Fields;

namespace iTunesMatchTagger.Core.Tracks;

/// <summary>
/// <see cref="ITaggableTrack"/> backed by a plain audio file - the
/// standalone mode that works without iTunes installed.
/// </summary>
public sealed class FileTrack : ITaggableTrack
{
    public FileTrack(string location)
    {
        Location = location;
        TrackId = TrackIdReader.TryReadTrackId(location);
    }

    public string Location { get; }

    public long TrackId { get; }

    public bool IsMatched => TrackId > 0;

    public object? ReadField(TrackField field)
    {
        if (field.GetFromFileTag is null)
        {
            return null;
        }

        try
        {
            using var file = TagLib.File.Create(Location);
            return field.GetFromFileTag(file.Tag);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void WriteField(TrackField field, object? value)
    {
        WriteFields([new KeyValuePair<TrackField, object?>(field, value)]);
    }

    /// <summary>Opens the file once, applies every write, saves once.</summary>
    public void WriteFields(IReadOnlyList<KeyValuePair<TrackField, object?>> writes)
    {
        if (writes.Count == 0)
        {
            return;
        }

        using var file = TagLib.File.Create(Location);
        foreach (var write in writes)
        {
            var field = write.Key;
            if (field.SetOnFileTag is null)
            {
                continue;
            }

            var writeValue = field.CoerceWrite?.Invoke(write.Value) ?? write.Value;
            if (writeValue is null)
            {
                continue;
            }

            field.SetOnFileTag(file.Tag, writeValue);
        }

        file.Save();
    }
}
