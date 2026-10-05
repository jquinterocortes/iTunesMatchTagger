using System.ComponentModel;
using iTunesMatchTagger.Core.Fields;
using iTunesMatchTagger.Core.ITunes;
using iTunesMatchTagger.Core.Tracks;

namespace iTunesMatchTagger.App;

/// <summary>
/// One row of the tracks grid: current tag values as display strings plus
/// lookup state. Values are strings everywhere so the grid, the manual
/// override column and both track back ends share one representation.
/// </summary>
public sealed class TrackRow : INotifyPropertyChanged
{
    /// <summary>Grid column (DataPropertyName) for each field's LookupMember.</summary>
    public static readonly IReadOnlyDictionary<string, string> PropertyByLookupMember =
        new Dictionary<string, string>
        {
            ["trackName"] = nameof(TrackName),
            ["artistName"] = nameof(ArtistName),
            ["AlbumArtist"] = nameof(AlbumArtist),
            ["collectionName"] = nameof(Album),
            ["year"] = nameof(Year),
            ["primaryGenreName"] = nameof(Genre),
            ["trackNumber"] = nameof(TrackNumber),
            ["trackCount"] = nameof(TrackCount),
            ["discNumber"] = nameof(DiscNumber),
            ["discCount"] = nameof(DiscCount),
            ["artworkUrl100"] = nameof(Artwork),
            ["Filename"] = nameof(File),
        };

    private string? _trackName;
    private string? _artistName;
    private string? _albumArtist;
    private string? _album;
    private string? _year;
    private string? _genre;
    private string? _trackNumber;
    private string? _trackCount;
    private string? _discNumber;
    private string? _discCount;
    private string? _artwork;

    public TrackRow(ITaggableTrack track)
    {
        Track = track;
        foreach (var field in TrackFields.All)
        {
            SetValue(field.LookupMember, track.ReadField(field)?.ToString());
        }
    }

    public ITaggableTrack Track { get; }

    public bool LookupSuccess { get; set; }

    public string File => Track.Location ?? "[unknown]";

    public string TrackId => Track.TrackId > 0 ? Track.TrackId.ToString() : string.Empty;

    public string? TrackName { get => _trackName; private set { _trackName = value; RaisePropertyChanged(nameof(TrackName)); } }

    public string? ArtistName { get => _artistName; private set { _artistName = value; RaisePropertyChanged(nameof(ArtistName)); } }

    public string? AlbumArtist { get => _albumArtist; private set { _albumArtist = value; RaisePropertyChanged(nameof(AlbumArtist)); } }

    public string? Album { get => _album; private set { _album = value; RaisePropertyChanged(nameof(Album)); } }

    public string? Year { get => _year; private set { _year = value; RaisePropertyChanged(nameof(Year)); } }

    public string? Genre { get => _genre; private set { _genre = value; RaisePropertyChanged(nameof(Genre)); } }

    public string? TrackNumber { get => _trackNumber; private set { _trackNumber = value; RaisePropertyChanged(nameof(TrackNumber)); } }

    public string? TrackCount { get => _trackCount; private set { _trackCount = value; RaisePropertyChanged(nameof(TrackCount)); } }

    public string? DiscNumber { get => _discNumber; private set { _discNumber = value; RaisePropertyChanged(nameof(DiscNumber)); } }

    public string? DiscCount { get => _discCount; private set { _discCount = value; RaisePropertyChanged(nameof(DiscCount)); } }

    public string? Artwork { get => _artwork; private set { _artwork = value; RaisePropertyChanged(nameof(Artwork)); } }

    public string? GetValue(string lookupMember) => lookupMember switch
    {
        "trackName" => TrackName,
        "artistName" => ArtistName,
        "AlbumArtist" => AlbumArtist,
        "collectionName" => Album,
        "year" => Year,
        "primaryGenreName" => Genre,
        "trackNumber" => TrackNumber,
        "trackCount" => TrackCount,
        "discNumber" => DiscNumber,
        "discCount" => DiscCount,
        "artworkUrl100" => Artwork,
        "Filename" => File,
        _ => null,
    };

    public void SetValue(string lookupMember, string? value)
    {
        switch (lookupMember)
        {
            case "trackName": TrackName = value; break;
            case "artistName": ArtistName = value; break;
            case "AlbumArtist": AlbumArtist = value; break;
            case "collectionName": Album = value; break;
            case "year": Year = value; break;
            case "primaryGenreName": Genre = value; break;
            case "trackNumber": TrackNumber = value; break;
            case "trackCount": TrackCount = value; break;
            case "discNumber": DiscNumber = value; break;
            case "discCount": DiscCount = value; break;
            case "artworkUrl100": Artwork = value; break;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaisePropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
