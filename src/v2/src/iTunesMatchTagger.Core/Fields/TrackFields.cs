using System.Globalization;

namespace iTunesMatchTagger.Core.Fields;

/// <summary>
/// The catalog of taggable fields, with the same defaults as the upstream
/// <c>MainForm.InitUpdateOptions()</c>: name, artist, album artist, album,
/// year and genre are written by default; numbering fields are opt-in.
/// </summary>
public static class TrackFields
{
    public static readonly TrackField TrackName =
        new("Track Name", "trackName",
            r => r.TrackName,
            t => (string?)t.Name,
            (t, v) => t.Name = v,
            GetFromFileTag: t => t.Title,
            SetOnFileTag: (t, v) => t.Title = AsString(v),
            UpdateByDefault: true);

    public static readonly TrackField ArtistName =
        new("Track Artist", "artistName",
            r => r.ArtistName,
            t => (string?)t.Artist,
            (t, v) => t.Artist = v,
            GetFromFileTag: t => t.Performers.FirstOrDefault(),
            SetOnFileTag: (t, v) => t.Performers = AsArray(v),
            UpdateByDefault: true);

    public static readonly TrackField AlbumArtist =
        new("Album Artist", "AlbumArtist",
            r => r.AlbumArtist,
            t => (string?)t.AlbumArtist,
            (t, v) => t.AlbumArtist = v,
            GetFromFileTag: t => t.AlbumArtists.FirstOrDefault(),
            SetOnFileTag: (t, v) => t.AlbumArtists = AsArray(v),
            UpdateByDefault: true);

    public static readonly TrackField AlbumName =
        new("Album Name", "collectionName",
            r => r.CollectionName,
            t => (string?)t.Album,
            (t, v) => t.Album = v,
            GetFromFileTag: t => t.Album,
            SetOnFileTag: (t, v) => t.Album = AsString(v),
            UpdateByDefault: true);

    public static readonly TrackField Year =
        new("Year", "year",
            r => r.Year,
            t => (int?)t.Year,
            (t, v) => t.Year = v,
            GetFromFileTag: t => t.Year > 0 ? (int?)t.Year : null,
            SetOnFileTag: (t, v) => t.Year = AsUint(v),
            CoerceWrite: CoerceInt,
            UpdateByDefault: true);

    public static readonly TrackField Genre =
        new("Genre", "primaryGenreName",
            r => r.PrimaryGenreName,
            t => (string?)t.Genre,
            (t, v) => t.Genre = v,
            GetFromFileTag: t => t.Genres.FirstOrDefault(),
            SetOnFileTag: (t, v) => t.Genres = AsArray(v),
            UpdateByDefault: true);

    public static readonly TrackField TrackNumber =
        new("Track Number", "trackNumber",
            r => r.TrackNumber,
            t => (int?)t.TrackNumber,
            (t, v) => t.TrackNumber = v,
            GetFromFileTag: t => t.Track > 0 ? (int?)t.Track : null,
            SetOnFileTag: (t, v) => t.Track = AsUint(v),
            CoerceWrite: CoerceInt);

    public static readonly TrackField TrackCount =
        new("Track Count", "trackCount",
            r => r.TrackCount,
            t => (int?)t.TrackCount,
            (t, v) => t.TrackCount = v,
            GetFromFileTag: t => t.TrackCount > 0 ? (int?)t.TrackCount : null,
            SetOnFileTag: (t, v) => t.TrackCount = AsUint(v),
            CoerceWrite: CoerceInt);

    public static readonly TrackField DiscNumber =
        new("Disc Number", "discNumber",
            r => r.DiscNumber,
            t => (int?)t.DiscNumber,
            (t, v) => t.DiscNumber = v,
            GetFromFileTag: t => t.Disc > 0 ? (int?)t.Disc : null,
            SetOnFileTag: (t, v) => t.Disc = AsUint(v),
            CoerceWrite: CoerceInt);

    public static readonly TrackField DiscCount =
        new("Disc Count", "discCount",
            r => r.DiscCount,
            t => (int?)t.DiscCount,
            (t, v) => t.DiscCount = v,
            GetFromFileTag: t => t.DiscCount > 0 ? (int?)t.DiscCount : null,
            SetOnFileTag: (t, v) => t.DiscCount = AsUint(v),
            CoerceWrite: CoerceInt);

    /// <summary>
    /// Read-only file location, shown as a grid column only (mirrors the
    /// upstream "Filename" option with <c>ShowOption = false</c>).
    /// </summary>
    public static readonly TrackField Filename =
        new("Filename", "Filename",
            _ => null,
            t => (string?)t.Location,
            SetOnItunes: null,
            NullValue: "[unknown]",
            VisibleInOptions: false);

    public static readonly IReadOnlyList<TrackField> All =
    [
        TrackName, ArtistName, AlbumArtist, AlbumName, Year, Genre,
        TrackNumber, TrackCount, DiscNumber, DiscCount, Filename,
    ];

    /// <summary>Fields offered in the "fields to update" grid.</summary>
    public static IEnumerable<TrackField> Visible => All.Where(f => f.VisibleInOptions);

    internal static object? CoerceInt(object? value) =>
        int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static string AsString(object? value) => value?.ToString() ?? string.Empty;

    private static string[] AsArray(object? value)
    {
        var text = AsString(value);
        return text.Length > 0 ? [text] : [];
    }

    private static uint AsUint(object? value) =>
        uint.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0u;
}
