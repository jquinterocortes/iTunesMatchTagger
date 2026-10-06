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

    public int? DurationMs
    {
        get
        {
            try
            {
                using var file = TagLib.File.Create(Location);
                return (int?)file.Properties.Duration.TotalMilliseconds;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public void WriteLyrics(string lyrics)
    {
        using var file = TagLib.File.Create(Location);
        file.Tag.Lyrics = lyrics;
        file.Save();
    }

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

    public void WriteArtwork(byte[] imageBytes)
    {
        var (mimeType, extension) = DetectImageType(imageBytes);
        var tempPath = Path.Combine(Path.GetTempPath(), $"imt-artwork-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(tempPath, imageBytes);
        try
        {
            using var file = TagLib.File.Create(Location);
            var picture = new TagLib.Picture(tempPath)
            {
                Type = TagLib.PictureType.FrontCover,
                MimeType = mimeType,
                Description = "Cover",
            };
            file.Tag.Pictures = new TagLib.IPicture[] { picture }; // replaces all existing artwork
            file.Save();
        }
        finally
        {
            try { File.Delete(tempPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static (string MimeType, string Extension) DetectImageType(byte[] data) =>
        data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 ? ("image/jpeg", ".jpg")
        : data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 ? ("image/png", ".png")
        : ("image/jpeg", ".jpg");
}
