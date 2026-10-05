using System.ComponentModel;
using iTunesMatchTagger.Core.Fields;
using iTunesMatchTagger.Core.Tracks;

namespace iTunesMatchTagger.App;

/// <summary>Severity used for coloring status text in the list and detail panel.</summary>
public enum StatusKind
{
    Neutral,
    Success,
    Warning,
    Error,
}

/// <summary>
/// One track in the validator: <b>current</b> values (read from iTunes/the
/// files), <b>proposed</b> values (from the lookup), artwork images and a
/// status line. Values are strings everywhere so the owner-drawn list, the
/// comparison table and both track back ends share one representation.
/// </summary>
public sealed class TrackRow : INotifyPropertyChanged
{
    private readonly Dictionary<string, string?> _current = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _proposed = new(StringComparer.Ordinal);
    private string _statusMessage = string.Empty;
    private StatusKind _statusSeverity = StatusKind.Neutral;
    private Image? _artworkImage;
    private Image? _currentArtworkImage;

    public TrackRow(ITaggableTrack track)
    {
        Track = track;
        foreach (var field in TrackFields.All)
        {
            _current[field.LookupMember] = track.ReadField(field)?.ToString();
        }
    }

    public ITaggableTrack Track { get; }

    /// <summary>True when the last lookup produced a result for this track.</summary>
    public bool LookupSuccess { get; set; }

    public string File => Track.Location ?? "[unknown]";

    public string TrackIdText => Track.TrackId > 0 ? Track.TrackId.ToString() : string.Empty;

    public string StatusMessage => _statusMessage;

    public StatusKind StatusSeverity => _statusSeverity;

    /// <summary>Artwork preview from the lookup result (or null).</summary>
    public Image? ArtworkImage => _artworkImage;

    /// <summary>Artwork currently embedded in the file (or null).</summary>
    public Image? CurrentArtworkImage => _currentArtworkImage;

    public string? GetCurrent(string lookupMember) =>
        _current.TryGetValue(lookupMember, out var value) ? value : null;

    public string? GetProposed(string lookupMember) =>
        _proposed.TryGetValue(lookupMember, out var value) ? value : null;

    public void SetCurrent(string lookupMember, string? value)
    {
        if (string.Equals(GetCurrent(lookupMember), value, StringComparison.Ordinal))
        {
            return;
        }

        _current[lookupMember] = value;
        Raise();
    }

    public void SetProposed(string lookupMember, string? value)
    {
        _proposed[lookupMember] = value;
        Raise();
    }

    public void ClearProposed()
    {
        _proposed.Clear();
        Raise();
    }

    public void SetStatus(string message, StatusKind severity)
    {
        _statusMessage = message;
        _statusSeverity = severity;
        Raise();
    }

    public void SetArtworkImage(Image? image)
    {
        _artworkImage?.Dispose();
        _artworkImage = image;
        Raise();
    }

    public void SetCurrentArtworkImage(Image? image)
    {
        _currentArtworkImage?.Dispose();
        _currentArtworkImage = image;
        Raise();
    }

    public void DisposeImages()
    {
        _artworkImage?.Dispose();
        _artworkImage = null;
        _currentArtworkImage?.Dispose();
        _currentArtworkImage = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string propertyName = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
