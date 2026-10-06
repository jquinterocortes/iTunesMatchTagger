using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using iTunesMatchTagger.Core.Fields;
using iTunesMatchTagger.Core.Lookup;
using iTunesMatchTagger.Core.Settings;
using iTunesMatchTagger.Core.Sources;

namespace iTunesMatchTagger.Wpf.ViewModels;

/// <summary>Editable copy of <see cref="AppSettings"/> for the Settings dialog.</summary>
public sealed class SettingsViewModel
{
    private readonly AppSettings _clone;

    private SettingsViewModel(AppSettings clone)
    {
        _clone = clone;
        Sources = [.. TagSourceCatalog.All
            .Where(i => i.Id != TagSources.ITunes)
            .Select(info =>
            {
                var saved = clone.Sources.FirstOrDefault(s => s.Id == info.Id);
                return new SourceSettingViewModel(info, saved?.Enabled == true, saved?.Token);
            })];

        CountriesText = string.Join(", ", clone.SelectedCountries);
        Fields = [.. TrackFields.All
            .Where(f => f.VisibleInOptions)
            .Select(f =>
            {
                var saved = clone.Fields.GetValueOrDefault(f.LookupMember);
                return new OptionRowViewModel(f, saved?.Update ?? f.UpdateByDefault, saved?.Overwrite ?? false, saved?.OverwriteValue);
            })];
    }

    /// <summary>Deep copy through JSON so edits are discardable via Cancel.</summary>
    public static SettingsViewModel FromSettings(AppSettings settings) =>
        new(JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings)) ?? new AppSettings());

    public IReadOnlyList<SourceSettingViewModel> Sources { get; }

    public ObservableCollection<OptionRowViewModel> Fields { get; }

    public bool AlwaysQueryAll
    {
        get => _clone.QueryAllSources;
        set => _clone.QueryAllSources = value;
    }

    public bool IncludeLyrics
    {
        get => _clone.IncludeLyrics;
        set => _clone.IncludeLyrics = value;
    }

    public string CountriesText { get; set; } = string.Empty;

    /// <summary>Parses, validates and applies the dialog state. Returns an error message or null.</summary>
    public string? Validate()
    {
        var codes = CountriesText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
        var unknown = codes.Where(c => !StoreCountries.All.Contains(c)).ToList();
        if (unknown.Count > 0)
        {
            return $"Unknown storefront code(s): {string.Join(", ", unknown)}";
        }

        if (codes.Count == 0)
        {
            return "Add at least one storefront (e.g. \"CO, US, JP\").";
        }

        _clone.SelectedCountries = codes;
        return null;
    }

    public void ApplyTo(AppSettings target)
    {
        target.QueryAllSources = _clone.QueryAllSources;
        target.IncludeLyrics = _clone.IncludeLyrics;
        target.SelectedCountries = [.. _clone.SelectedCountries];
        target.Sources =
        [
            new TagSourceSettings { Id = TagSources.ITunes, Enabled = true, Token = null },
            .. Sources.Select(static s => new TagSourceSettings
            {
                Id = s.Info.Id,
                Enabled = s.IsChecked,
                Token = s.Info.Id == TagSources.Discogs ? s.Token : null,
            }),
        ];
        target.Fields.Clear();
        foreach (var field in Fields)
        {
            target.Fields[field.Field.LookupMember] = new AppSettings.FieldOptions
            {
                Update = field.Update,
                Overwrite = field.Overwrite,
                OverwriteValue = field.OverwriteValue,
            };
        }
    }
}

/// <summary>One row of the "Sources" tab.</summary>
public sealed class SourceSettingViewModel(TagSourceInfo info, bool isChecked, string? token) : INotifyPropertyChanged
{
    private bool _isChecked = isChecked;
    private string? _token = token;

    public TagSourceInfo Info { get; } = info;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Token
    {
        get => _token;
        set
        {
            if (!string.Equals(_token, value, StringComparison.Ordinal))
            {
                _token = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasTokenColumn => Info.Id == TagSources.Discogs;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
