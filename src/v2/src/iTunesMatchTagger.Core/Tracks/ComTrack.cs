using System.Reflection;
using System.Runtime.InteropServices;
using iTunesMatchTagger.Core.Fields;

namespace iTunesMatchTagger.Core.Tracks;

/// <summary>
/// <see cref="ITaggableTrack"/> backed by a live iTunes COM track object
/// (IITTrack). Property access is late-bound (IDispatch), so no COM
/// reference or interop assembly is needed at build time.
/// </summary>
public sealed class ComTrack : ITaggableTrack
{
    public ComTrack(dynamic raw)
    {
        Raw = raw;
        try
        {
            DatabaseId = (long)((int)raw.TrackDatabaseID);
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or TargetInvocationException)
        {
            DatabaseId = 0;
        }

        try
        {
            Location = (string?)raw.Location;
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or TargetInvocationException)
        {
            Location = null; // e.g. tracks not stored locally (iCloud-only)
        }

        TrackId = Location is null ? 0 : TrackIdReader.TryReadTrackId(Location);
    }

    public dynamic Raw { get; }

    /// <summary>iTunes' per-library database ID (the one shared by every playlist view of the song).</summary>
    public long DatabaseId { get; }

    public string? Location { get; }

    public long TrackId { get; }

    /// <summary>
    /// v2 improvement over upstream: a track counts as iTunes-Matched when
    /// its file carries the embedded catalog ID, instead of comparing the
    /// localized <c>KindAsString</c> against a hard-coded language list.
    /// </summary>
    public bool IsMatched => TrackId > 0;

    /// <summary>iTunes exposes the duration as seconds (numeric) and Time as a formatted string; prefer the number.</summary>
    public int? DurationMs
    {
        get
        {
            try
            {
                var seconds = (int)Raw.Duration;
                if (seconds > 0)
                {
                    return seconds * 1000;
                }

                return ParsePackedTime((string?)Raw.Time);
            }
            catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                return null;
            }
        }
    }

    public void WriteLyrics(string lyrics) => Raw.Lyrics = lyrics;

    private static int? ParsePackedTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Split(':');
        if (parts.Length is < 2 or > 3)
        {
            return null;
        }

        try
        {
            var seconds = parts.Length == 3
                ? int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) * 3600
                  + int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) * 60
                  + int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)
                : int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) * 60
                  + int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            return seconds * 1000;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public object? ReadField(TrackField field)
    {
        try
        {
            return field.GetFromItunes(Raw);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void WriteField(TrackField field, object? value)
    {
        if (field.SetOnItunes is null)
        {
            return;
        }

        var writeValue = field.CoerceWrite?.Invoke(value) ?? value;
        if (writeValue is null)
        {
            return;
        }

        field.SetOnItunes(Raw, writeValue);
    }

    public void WriteArtwork(byte[] imageBytes)
    {
        // iTunes embeds the image into the file for file-backed tracks.
        // Replace (not append): matched files usually already carry artwork.
        var tempPath = Path.Combine(Path.GetTempPath(), $"imt-artwork-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(tempPath, imageBytes);
        try
        {
            dynamic artworks = Raw.Artwork;
            int existing = (int)artworks.Count;
            for (int i = existing; i >= 1; i--) // 1-based collection
            {
                artworks[i].Delete();
            }

            Raw.AddArtworkFromFile(tempPath);
        }
        finally
        {
            try { File.Delete(tempPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
