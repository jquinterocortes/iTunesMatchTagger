using System.ComponentModel;
using iTunesMatchTagger.Core.Fields;
using iTunesMatchTagger.Core.Sources;
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
    private readonly HashSet<string> _enabledFields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _sourcePick = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Image?> _candidateArtwork = new();
    private IReadOnlyList<TagCandidate> _candidates = [];
    private int _activeCandidateIndex = -1;
    private string _statusMessage = string.Empty;
    private StatusKind _statusSeverity = StatusKind.Neutral;
    private Image? _currentArtworkImage;

    public TrackRow(ITaggableTrack track)
    {
        Track = track;
        foreach (var field in TrackFields.All)
        {
            _current[field.LookupMember] = track.ReadField(field)?.ToString();
        }

        // per-row field mask starts matching the global options grid
        foreach (var field in TrackFields.All)
        {
            _enabledFields.Add(field.LookupMember);
        }
    }

    public ITaggableTrack Track { get; }

    /// <summary>True when the last lookup produced a result for this track.</summary>
    public bool LookupSuccess { get; set; }

    public string File => Track.Location ?? "[unknown]";

    public string TrackIdText => Track.TrackId > 0 ? Track.TrackId.ToString() : string.Empty;

    public string StatusMessage => _statusMessage;

    public StatusKind StatusSeverity => _statusSeverity;

    /// <summary>Artwork preview of the active candidate (or null).</summary>
    public Image? ArtworkImage =>
        ActiveCandidateIndex >= 0 && _candidateArtwork.TryGetValue(ActiveCandidateIndex, out var image)
            ? image
            : null;

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
    /// active one (the lookup's best guess: the Apple hit, or the first
    /// fallback candidate). Proposed values are recomputed from the active
    /// candidate. Must be called on the UI thread.
    /// </summary>
    public void SetCandidates(IReadOnlyList<TagCandidate> candidates, int autoSelectIndex)
    {
        foreach (var image in _candidateArtwork.Values)
        {
            image?.Dispose();
        }

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

        for (var i = 0; i < _candidates.Count; i++)
        {
            if (_candidates[i].SourceId == sourceId)
            {
                return i;
            }
        }

        return -1;
    }

    public TagCandidate? CandidateForSource(string sourceId)
    {
        var index = CandidateIndexForSource(sourceId);
        return index < 0 ? null : _candidates[index];
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

    public void SetStatus(string message, StatusKind severity)
    {
        _statusMessage = message;
        _statusSeverity = severity;
        Raise();
    }

    public void SetCurrentArtworkImage(Image? image)
    {
        _currentArtworkImage?.Dispose();
        _currentArtworkImage = image;
        Raise();
    }

    /// <summary>Pairs a candidate index with its artwork preview. UI thread.</summary>
    public void SetCandidateArtwork(int candidateIndex, Image? image)
    {
        if (candidateIndex < 0 || candidateIndex >= _candidates.Count)
        {
            image?.Dispose();
            return;
        }

        if (_candidateArtwork.TryGetValue(candidateIndex, out var old) && !ReferenceEquals(old, image))
        {
            old?.Dispose();
        }

        _candidateArtwork[candidateIndex] = image;
        Raise();
    }

    public Image? GetCandidateArtwork(int candidateIndex) =>
        candidateIndex >= 0 && _candidateArtwork.TryGetValue(candidateIndex, out var image) ? image : null;

    public void DisposeImages()
    {
        foreach (var image in _candidateArtwork.Values)
        {
            image?.Dispose();
        }

        _candidateArtwork.Clear();
        _currentArtworkImage?.Dispose();
        _currentArtworkImage = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string propertyName = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
