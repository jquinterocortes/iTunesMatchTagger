using System.ComponentModel;
using iTunesMatchTagger.Core.Fields;
using iTunesMatchTagger.Core.Sources;

namespace iTunesMatchTagger.Wpf.ViewModels;

/// <summary>Global field options ("Settings > Fields"), the WinForms OptionRow ported.</summary>
public sealed class OptionRowViewModel : INotifyPropertyChanged
{
    public OptionRowViewModel(TrackField field, bool update, bool overwrite, string? overwriteValue)
    {
        Field = field;
        _update = update;
        _overwrite = overwrite;
        _overwriteValue = overwriteValue;
    }

    public TrackField Field { get; }

    public string FieldName => Field.DisplayName;

    private bool _update;

    public bool Update
    {
        get => _update;
        set
        {
            if (_update != value)
            {
                _update = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _overwrite;

    public bool Overwrite
    {
        get => _overwrite;
        set
        {
            if (_overwrite != value)
            {
                _overwrite = value;
                OnPropertyChanged();
            }
        }
    }

    private string? _overwriteValue;

    public string? OverwriteValue
    {
        get => _overwriteValue;
        set
        {
            if (!string.Equals(_overwriteValue, value, StringComparison.Ordinal))
            {
                _overwriteValue = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>One comparison cell: the source's value and whether it differs from the current tag.</summary>
public sealed record SourceCell(string Text, bool IsDifferent);

/// <summary>
/// One comparison row of the detail panel: the field, the current value and
/// one column per tag source. <see cref="Use"/> is the per-track field mask.
/// </summary>
public sealed class FieldComparisonRowViewModel : INotifyPropertyChanged
{
    private readonly TrackRowViewModel _row;
    private readonly IReadOnlyList<string> _sourceIds;
    private readonly Dictionary<string, SourceCell> _sourceCells;

    public FieldComparisonRowViewModel(TrackRowViewModel row, TrackField field, IReadOnlyList<string> sourceIds)
    {
        _row = row;
        Field = field;
        _sourceIds = sourceIds;
        _sourceCells = sourceIds.ToDictionary(static s => s, static _ => new SourceCell(string.Empty, false));

        Refresh();
    }

    public TrackField Field { get; }

    public string FieldName => Field.DisplayName;

    /// <summary>Per-track field mask (writes only when checked "Use").</summary>
    public bool Use
    {
        get => _row.IsFieldEnabled(Field.LookupMember);
        set => _row.SetFieldEnabled(Field.LookupMember, value);
    }

    public string Current
    {
        get
        {
            var value = _row.GetCurrent(Field.LookupMember);
            return value ?? string.Empty;
        }
    }

    /// <summary>Per-source cells (indexed by source id, e.g. SourceCells[Apple]).</summary>
    public IReadOnlyDictionary<string, SourceCell> SourceCells => _sourceCells;

    /// <summary>Repaints every binding after any data change.</summary>
    public void Refresh()
    {
        foreach (var sourceId in _sourceIds)
        {
            var candidate = _row.CandidateForSource(sourceId);
            var text = candidate is null
                ? string.Empty
                : Field.GetFromCandidate?.Invoke(candidate)?.ToString() ?? string.Empty;
            _sourceCells[sourceId] = new SourceCell(text, !string.Equals(text, Current, StringComparison.Ordinal));
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>ComboBox friendly wrapper around a candidate.</summary>
public sealed record CandidateOptionViewModel(int Index, TrackRowViewModel Row)
{
    public string DisplayName
    {
        get
        {
            var candidate = Row.Candidates[Index];
            var source = candidate.SourceId == TagSources.ITunes ? "Apple" : candidate.SourceId;
            var album = string.IsNullOrWhiteSpace(candidate.Album) ? string.Empty : $" — {candidate.Album}";
            var details = string.IsNullOrWhiteSpace(candidate.Details) ? string.Empty : $" ({candidate.Details})";
            return $"{source}: {candidate.Title}{album}{details}";
        }
    }
}


