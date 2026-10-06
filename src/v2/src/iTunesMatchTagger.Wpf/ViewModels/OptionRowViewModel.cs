using System.ComponentModel;
using System.Windows.Media.Imaging;
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

/// <summary>
/// One comparison cell: the source's value and whether it differs from the
/// current tag. <see cref="Image"/> replaces the text when set (artwork row).
/// </summary>
public sealed record SourceCell(string Text, bool IsDifferent, BitmapImage? Image = null);

/// <summary>
/// One comparison row of the detail panel: the field, the current value and
/// one column per tag source. <see cref="Use"/> is the per-track field mask.
/// The Album Artwork field renders image previews instead of URL text.
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
        IsArtworkRow = Field == TrackFields.Artwork;

        Refresh();
    }

    public TrackField Field { get; }

    /// <summary>The artwork row renders images instead of text.</summary>
    public bool IsArtworkRow { get; }

    public string FieldName => Field.DisplayName;

    /// <summary>Per-track field mask (writes only when checked "Use").</summary>
    public bool Use
    {
        get => _row.IsFieldEnabled(Field.LookupMember);
        set => _row.SetFieldEnabled(Field.LookupMember, value);
    }

    public SourceCell CurrentCell { get; private set; } = new(string.Empty, false);

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
        CurrentCell = IsArtworkRow
            ? new SourceCell(string.Empty, false, Imaging.FromBytes(_row.CurrentArtwork))
            : new SourceCell(Current, false);

        foreach (var sourceId in _sourceIds)
        {
            var candidate = _row.CandidateForSource(sourceId);
            var text = candidate is null
                ? string.Empty
                : Field.GetFromCandidate?.Invoke(candidate)?.ToString() ?? string.Empty;

            var image = IsArtworkRow && candidate is not null
                ? Imaging.FromBytes(_row.GetCandidateArtwork(_row.CandidateIndexForSource(sourceId)))
                : null;

            _sourceCells[sourceId] = new SourceCell(text, !string.Equals(text, Current, StringComparison.Ordinal), image);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>ComboBox friendly wrapper around a candidate (global pick).</summary>
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

/// <summary>
/// One of one source's candidates for that source's header dropdown: index is
/// within <see cref="TrackRowViewModel.CandidatesForSource"/> and the display
/// omits the source prefix (the column header already says it).
/// </summary>
public sealed record SourceCandidateOption(int IndexInSource, TagCandidate Candidate)
{
    public string DisplayName
    {
        get
        {
            var album = string.IsNullOrWhiteSpace(Candidate.Album) ? string.Empty : $" — {Candidate.Album}";
            var details = string.IsNullOrWhiteSpace(Candidate.Details) ? string.Empty : $" ({Candidate.Details})";
            return $"{Candidate.Title}{album}{details}";
        }
    }
}


