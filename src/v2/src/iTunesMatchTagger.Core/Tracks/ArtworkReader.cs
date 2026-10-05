namespace iTunesMatchTagger.Core.Tracks;

/// <summary>
/// Reads artwork already embedded in an audio file (front cover preferred).
/// Works in both modes: in iTunes mode the matched file carries the cover,
/// so the app can show "current vs from Apple" without touching COM.
/// </summary>
public static class ArtworkReader
{
    /// <summary>Returns the embedded cover image bytes, or null when the file has none / can't be read.</summary>
    public static byte[]? ReadFrontCover(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        try
        {
            using var file = TagLib.File.Create(filePath);
            var pictures = file.Tag.Pictures;
            var picture = pictures.FirstOrDefault(static p => p.Type == TagLib.PictureType.FrontCover)
                          ?? pictures.FirstOrDefault();
            return picture?.Data?.Data;
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or TagLib.CorruptFileException
                                       or NotSupportedException)
        {
            return null;
        }
    }
}
