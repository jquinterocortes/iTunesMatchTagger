using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using iTunesMatchTagger.Core.Fields;
using iTunesMatchTagger.Core.Sources;
using iTunesMatchTagger.Core.Tracks;

namespace iTunesMatchTagger.Wpf.ViewModels;

/// <summary>Severity used for coloring status text (list, detail and log).</summary>
public enum StatusKind
{
    Neutral,
    Success,
    Warning,
    Error,
}

/// <summary>One track in the validator - the WinForms TrackRow ported to WPF.</summary>
public sealed class TrackRowViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<string, string?> _current = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _proposed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _enabledFields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _sourcePick = new(StringComparer.Ordinal);
    private readonly Dictionary<int, byte[]> _candidateArtwork = new();
    private IReadOnlyList<TagCandidate> _candidates = [];
    private int _activeCandidateIndex = -1;
    private string _statusMessage = string.Empty;
    private StatusKind _statusSeverity = StatusKind.Neutral;
    private byte[]? _currentArtwork;

    public TrackRowViewModel(ITaggableTrack track)
    {
        Track = track;
        foreach (var field in TrackFields.All)
        {
            _current[field.LookupMember] = track.ReadField(field)?.ToString();
            _enabledFields.Add(field.LookupMember);
        }

        RefreshThumbnailActions();
    }

    public ITaggableTrack Track { get; }

    /// <summary>True when the last lookup produced a result for this track.</summary>
    public bool LookupSuccess { get; set; }

    public string File => Track.Location ?? "[unknown]";

    private bool _skipUpdate;

    /// <summary>User exclusion from "3. Update tracks" (per-track toggle).</summary>
    public bool SkipUpdate
    {
        get => _skipUpdate;
        set
        {
            if (_skipUpdate != value)
            {
                _skipUpdate = value;
                Raise();
            }
        }
    }

    public string TrackIdText => Track.TrackId > 0 ? Track.TrackId.ToString() : string.Empty;

    public string StatusMessage => _statusMessage;

    public StatusKind StatusSeverity => _statusSeverity;

    /// <summary>Identifier of the active candidate's source (badge in the list).</summary>
    public string SourceBadge
    {
        get
        {
            var candidate = ActiveCandidate;
            if (candidate is null)
            {
                return string.Empty;
            }

            return candidate.SourceId == TagSources.ITunes ? "Apple" : candidate.SourceId;
        }
    }

    /// <summary>List row caption: "Title — Artist".</summary>
    public string Caption
    {
        get
        {
            var title = GetCurrent("trackName");
            var artist = GetCurrent("artistName");
            return string.Join(" — ", new[] { title, artist }.Where(static s => !string.IsNullOrWhiteSpace(s)));
        }
    }

    /// <summary>Candidate picker items (Apple hit first, then each source).</summary>
    public IReadOnlyList<CandidateOptionViewModel> CandidateOptions
    {
        get
        {
            var options = new List<CandidateOptionViewModel>(_candidates.Count);
            for (var i = 0; i < _candidates.Count; i++)
            {
                options.Add(new CandidateOptionViewModel(i, this));
            }

            return options;
        }
    }

    // ------------------------------------------------------------------
    // Values
    // ------------------------------------------------------------------

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
        RefreshThumbnailActions();
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

    /// <summary>Re-reads every current value from the file/COM object and repaints.</summary>
    public void RefreshCurrentValues()
    {
        foreach (var field in TrackFields.All)
        {
            _current[field.LookupMember] = Track.ReadField(field)?.ToString();
        }

        RefreshThumbnailActions();
        Raise();
    }

    // ------------------------------------------------------------------
    // Candidates (one lookup may offer several, across sources)
    // ------------------------------------------------------------------

    public IReadOnlyList<TagCandidate> Candidates => _candidates;

    /// <summary>The candidate whose values are proposed for writing.</summary>
    public int ActiveCandidateIndex => _activeCandidateIndex;

    public TagCandidate? ActiveCandidate =>
        _activeCandidateIndex >= 0 && _activeCandidateIndex < _candidates.Count
            ? _candidates[_activeCandidateIndex]
            : null;

    /// <summary>
    /// Replaces the candidate list and marks each source's first candidate
    /// as that source's pick and <paramref name="autoSelectIndex"/> as the
    /// active one. Must run on the UI thread.
    /// </summary>
    public void SetCandidates(IReadOnlyList<TagCandidate> candidates, int autoSelectIndex)
    {
        _candidateArtwork.Clear();
        _sourcePick.Clear();
        _candidates = candidates;
        for (var i = 0; i < candidates.Count; i++)
        {
            _sourcePick.TryAdd(candidates[i].SourceId, i);
        }

        _activeCandidateIndex = candidates.Count == 0
            ? -1
            : Math.Clamp(autoSelectIndex, 0, candidates.Count - 1);
        ApplyActiveCandidate();
    }

    /// <summary>
    /// Marks candidate <paramref name="index"/> active; it also becomes the
    /// pick of its source, so that source's comparison column shows it.
    /// </summary>
    public void SelectCandidate(int index)
    {
        if (index < -1 || index >= _candidates.Count)
        {
            return;
        }

        _activeCandidateIndex = index;
        if (index >= 0)
        {
            _sourcePick[_candidates[index].SourceId] = index;
        }

        ApplyActiveCandidate();
    }

    /// <summary>
    /// Index of the candidate currently shown for <paramref name="sourceId"/>:
    /// the user's pick within that source, else its first candidate; -1 when
    /// the source produced nothing.
    /// </summary>
    public int CandidateIndexForSource(string sourceId)
    {
        if (_sourcePick.TryGetValue(sourceId, out var picked) &&
            picked >= 0 && picked < _candidates.Count &&
            _candidates[picked].SourceId == sourceId)
        {
            return picked;
        }

        return FindFirstCandidateIndexForSource(sourceId);
    }

    public TagCandidate? CandidateForSource(string sourceId)
    {
        var index = CandidateIndexForSource(sourceId);
        return index < 0 ? null : _candidates[index];
    }

    private int FindFirstCandidateIndexForSource(string sourceId)
    {
        for (var i = 0; i < _candidates.Count; i++)
        {
            if (_candidates[i].SourceId == sourceId)
            {
                return i;
            }
        }

        return -1;
    }

    private void ApplyActiveCandidate()
    {
        _proposed.Clear();
        if (ActiveCandidate is { } candidate)
        {
            foreach (var field in TrackFields.All)
            {
                if (field.GetFromCandidate is { } getValue)
                {
                    _proposed[field.LookupMember] = getValue(candidate)?.ToString();
                }
            }
        }

        Raise();
    }

    // ------------------------------------------------------------------
    // Per-row field mask (which fields the update step may write)
    // ------------------------------------------------------------------

    public bool IsFieldEnabled(string lookupMember) => _enabledFields.Contains(lookupMember);

    public void SetFieldEnabled(string lookupMember, bool enabled)
    {
        var changed = enabled ? _enabledFields.Add(lookupMember) : _enabledFields.Remove(lookupMember);
        if (changed)
        {
            Raise();
        }
    }

    // ------------------------------------------------------------------
    // Artwork (byte arrays; WPF decodes on demand)
    // ------------------------------------------------------------------

    /// <summary>Artwork currently embedded in the file (or null).</summary>
    public byte[]? CurrentArtwork => _currentArtwork;

    /// <summary>Candidate previews keyed by candidate index (300px). From the lookup.</summary>
    public IReadOnlyDictionary<int, byte[]> CandidateArtwork => _candidateArtwork;

    public void SetCandidateArtwork(int candidateIndex, byte[]? bytes)
    {
        if (candidateIndex < 0 || candidateIndex >= _candidates.Count)
        {
            return;
        }

        if (bytes is null)
        {
            _candidateArtwork.Remove(candidateIndex);
        }
        else
        {
            _candidateArtwork[candidateIndex] = bytes;
        }

        Raise();
    }

    public byte[]? GetCandidateArtwork(int candidateIndex) =>
        candidateIndex >= 0 && _candidateArtwork.TryGetValue(candidateIndex, out var bytes) ? bytes : null;

    public void SetCurrentArtwork(byte[]? bytes)
    {
        _currentArtwork = bytes;
        Raise();
    }

    /// <summary>Small row thumbnail, decoded from the embedded artwork on the fly.</summary>
    public BitmapImage? Thumbnail =>
        Track.Location is { Length: > 0 } path && _thumbnailFor is { } builder
            ? builder(path)
            : null;

    private Func<string, BitmapImage?>? _thumbnailFor;
    private byte[]? _lastThumbnailSide = [];

    private void RefreshThumbnailActions()
    {
        // cheap cache: rerender only when the embedded image actually changes
        var bytes = Track.Location is { Length: > 0 } path ? ArtworkReader.ReadFrontCover(path) : null;
        _thumbnailFor = BuildThumbnail;
        if (!ReferenceEquals(_lastThumbnailSide, bytes))
        {
            _lastThumbnailSide = bytes;
            _currentArtwork = bytes;
        }
    }

    private BitmapImage? BuildThumbnail(string path)
    {
        var bytes = _lastThumbnailSide ?? ArtworkReader.ReadFrontCover(path);
        return bytes is null ? null : Imaging.FromBytes(bytes);
    }

    public void SetStatus(string message, StatusKind severity)
    {
        _statusMessage = message;
        _statusSeverity = severity;
        Raise();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string propertyName = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>Image decoding helpers (WPF side).</summary>
public static class Imaging
{
    public static BitmapImage? FromBytes(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            using var stream = new MemoryStream(bytes);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.DecodePixelWidth = 120; // enough for thumbnails and previews
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
