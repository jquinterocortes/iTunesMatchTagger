using System.Globalization;
using iTunesMatchTagger.Core.Sources;

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
            GetFromLookup: r => r.TrackName,
            GetFromCandidate: c => c.Title,
            GetFromItunes: t => (string?)t.Name,
            SetOnItunes: (t, v) => t.Name = v,
            GetFromFileTag: t => t.Title,
            SetOnFileTag: (t, v) => t.Title = AsString(v),
            UpdateByDefault: true);

    public static readonly TrackField ArtistName =
        new("Track Artist", "artistName",
            GetFromLookup: r => r.ArtistName,
            GetFromCandidate: c => c.Artist,
            GetFromItunes: t => (string?)t.Artist,
            SetOnItunes: (t, v) => t.Artist = v,
            GetFromFileTag: t => t.Performers.FirstOrDefault(),
            SetOnFileTag: (t, v) => t.Performers = AsArray(v),
            UpdateByDefault: true);

    public static readonly TrackField AlbumArtist =
        new("Album Artist", "AlbumArtist",
            GetFromLookup: r => r.AlbumArtist,
            GetFromCandidate: c => c.AlbumArtist ?? c.Artist,
            GetFromItunes: t => (string?)t.AlbumArtist,
            SetOnItunes: (t, v) => t.AlbumArtist = v,
            GetFromFileTag: t => t.AlbumArtists.FirstOrDefault(),
            SetOnFileTag: (t, v) => t.AlbumArtists = AsArray(v),
            UpdateByDefault: true);

    public static readonly TrackField AlbumName =
        new("Album Name", "collectionName",
            GetFromLookup: r => r.CollectionName,
            GetFromCandidate: c => c.Album,
            GetFromItunes: t => (string?)t.Album,
            SetOnItunes: (t, v) => t.Album = v,
            GetFromFileTag: t => t.Album,
            SetOnFileTag: (t, v) => t.Album = AsString(v),
            UpdateByDefault: true);

    public static readonly TrackField Year =
        new("Year", "year",
            GetFromLookup: r => r.Year,
            GetFromCandidate: c => c.Year,
            GetFromItunes: t => (int?)t.Year,
            SetOnItunes: (t, v) => t.Year = v,
            GetFromFileTag: t => t.Year > 0 ? (int?)t.Year : null,
            SetOnFileTag: (t, v) => t.Year = AsUint(v),
            CoerceWrite: CoerceInt,
            UpdateByDefault: true);

    public static readonly TrackField Genre =
        new("Genre", "primaryGenreName",
            GetFromLookup: r => r.PrimaryGenreName,
            GetFromCandidate: c => c.Genre,
            GetFromItunes: t => (string?)t.Genre,
            SetOnItunes: (t, v) => t.Genre = v,
            GetFromFileTag: t => t.Genres.FirstOrDefault(),
            SetOnFileTag: (t, v) => t.Genres = AsArray(v),
            UpdateByDefault: true);

    public static readonly TrackField TrackNumber =
        new("Track Number", "trackNumber",
            GetFromLookup: r => r.TrackNumber,
            GetFromCandidate: c => c.TrackNumber,
            GetFromItunes: t => (int?)t.TrackNumber,
            SetOnItunes: (t, v) => t.TrackNumber = v,
            GetFromFileTag: t => t.Track > 0 ? (int?)t.Track : null,
            SetOnFileTag: (t, v) => t.Track = AsUint(v),
            CoerceWrite: CoerceInt,
            UpdateByDefault: true);

    public static readonly TrackField TrackCount =
        new("Track Count", "trackCount",
            GetFromLookup: r => r.TrackCount,
            GetFromCandidate: c => c.TrackCount,
            GetFromItunes: t => (int?)t.TrackCount,
            SetOnItunes: (t, v) => t.TrackCount = v,
            GetFromFileTag: t => t.TrackCount > 0 ? (int?)t.TrackCount : null,
            SetOnFileTag: (t, v) => t.TrackCount = AsUint(v),
            CoerceWrite: CoerceInt,
            UpdateByDefault: true);

    public static readonly TrackField DiscNumber =
        new("Disc Number", "discNumber",
            GetFromLookup: r => r.DiscNumber,
            GetFromCandidate: c => c.DiscNumber,
            GetFromItunes: t => (int?)t.DiscNumber,
            SetOnItunes: (t, v) => t.DiscNumber = v,
            GetFromFileTag: t => t.Disc > 0 ? (int?)t.Disc : null,
            SetOnFileTag: (t, v) => t.Disc = AsUint(v),
            CoerceWrite: CoerceInt,
            UpdateByDefault: true);

    public static readonly TrackField DiscCount =
        new("Disc Count", "discCount",
            GetFromLookup: r => r.DiscCount,
            GetFromCandidate: c => c.DiscCount,
            GetFromItunes: t => (int?)t.DiscCount,
            SetOnItunes: (t, v) => t.DiscCount = v,
            GetFromFileTag: t => t.DiscCount > 0 ? (int?)t.DiscCount : null,
            SetOnFileTag: (t, v) => t.DiscCount = AsUint(v),
            CoerceWrite: CoerceInt,
            UpdateByDefault: true);

    /// <summary>
    /// Album artwork, exposed by the Search API as artworkUrl100. The value
    /// is a URL; "3. Update tracks" downloads it (upgraded to 600x600) and
    /// writes it through <see cref="Tracks.ITaggableTrack.WriteArtwork"/>,
    /// so this field is special-cased in the app's update loop. Candidates
    /// from other sources carry a direct artwork URL.
    /// </summary>
    public static readonly TrackField Artwork =
        new("Album Artwork", "artworkUrl100",
            GetFromLookup: r => r.ArtworkUrl100,
            GetFromCandidate: c => c.ArtworkUrl,
            SetOnItunes: null,
            UpdateByDefault: true);

    /// <summary>
    /// Read-only file location, shown as a grid column only (mirrors the
    /// upstream "Filename" option with <c>ShowOption = false</c>).
    /// </summary>
    public static readonly TrackField Filename =
        new("Filename", "Filename",
            GetFromLookup: _ => null,
            GetFromCandidate: _ => null,
            GetFromItunes: t => (string?)t.Location,
            SetOnItunes: null,
            NullValue: "[unknown]",
            VisibleInOptions: false);

    public static readonly IReadOnlyList<TrackField> All =
    [
        TrackName, ArtistName, AlbumArtist, AlbumName, Year, Genre,
        TrackNumber, TrackCount, DiscNumber, DiscCount, Artwork, Filename,
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
