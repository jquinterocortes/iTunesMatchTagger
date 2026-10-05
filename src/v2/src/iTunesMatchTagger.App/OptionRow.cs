using System.ComponentModel;
using iTunesMatchTagger.Core.Fields;

namespace iTunesMatchTagger.App;

/// <summary>
/// One row of the "fields to update" grid: whether the field is written
/// during "3. Update tracks", and an optional manual override value.
/// </summary>
public sealed class OptionRow : INotifyPropertyChanged
{
    private bool _update;
    private bool _overwrite;
    private string? _overwriteValue;

    public OptionRow(TrackField field)
    {
        Field = field;
        _update = field.UpdateByDefault;
    }

    public TrackField Field { get; }

    public string FieldName => Field.DisplayName;

    public bool Update
    {
        get => _update;
        set
        {
            _update = value;
            RaisePropertyChanged(nameof(Update));
        }
    }

    public bool Overwrite
    {
        get => _overwrite;
        set
        {
            _overwrite = value;
            RaisePropertyChanged(nameof(Overwrite));
        }
    }

    public string? OverwriteValue
    {
        get => _overwriteValue;
        set
        {
            _overwriteValue = value;
            RaisePropertyChanged(nameof(OverwriteValue));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaisePropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
