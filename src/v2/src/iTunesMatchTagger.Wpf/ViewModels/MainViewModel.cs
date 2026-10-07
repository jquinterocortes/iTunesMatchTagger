using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using iTunesMatchTagger.Core.Fields;
using iTunesMatchTagger.Core.ITunes;
using iTunesMatchTagger.Core.Lookup;
using iTunesMatchTagger.Core.Settings;
using iTunesMatchTagger.Core.Sources;
using iTunesMatchTagger.Core.Tracks;
using iTunesMatchTagger.Wpf.Services;

namespace iTunesMatchTagger.Wpf.ViewModels;

/// <summary>The main screen: tracks, lookups, updates and the log.</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ITunesComClient _itunes = new();
    private readonly ITunesSearchClient _search = new();
    private readonly LyricsClient _lyrics = new();
    private readonly HttpClient _artworkHttp = new();
    private readonly ObservableCollection<LogLineViewModel> _logLines = [];

    private AppSettings _settings = AppSettings.Load();
    private bool _busy;
    private TrackRowViewModel? _selectedRow;
    private string _statusText = "Ready";
    private CancellationTokenSource? _cancellation;

    public MainViewModel()
    {
        Rows = [];
        LogLines = new ReadOnlyObservableCollection<LogLineViewModel>(_logLines);
        DetailRows = [];
        LoadOptionsFromSettings();

        GetTracksCommand = new RelayCommand(() => _ = GetTracksAsync(), () => !_busy);
        LoadFolderCommand = new RelayCommand(() => _ = LoadFolderAsync(), () => !_busy);
        LookupCommand = new RelayCommand(() => _ = LookupAsync(), () => CanLookup);
        UpdateCommand = new RelayCommand(() => _ = UpdateAsync(), () => CanUpdate);
        OpenSettingsCommand = new RelayCommand(OpenSettings, () => !_busy);
        CancelCommand = new RelayCommand(() => _cancellation?.Cancel(), () => _busy);
    }

    // ------------------------------------------------------------------
    // Collections + commands
    // ------------------------------------------------------------------

    public ObservableCollection<TrackRowViewModel> Rows { get; }

    public ReadOnlyObservableCollection<LogLineViewModel> LogLines { get; }

    public ObservableCollection<FieldComparisonRowViewModel> DetailRows { get; }

    public RelayCommand GetTracksCommand { get; }
    public RelayCommand LoadFolderCommand { get; }
    public RelayCommand LookupCommand { get; }
    public RelayCommand UpdateCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand CancelCommand { get; }

    public IReadOnlyList<OptionRowViewModel> Options { get; private set; } = [];

    public TrackRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (ReferenceEquals(_selectedRow, value))
            {
                return;
            }

            _selectedRow = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedRow)));
            RebuildDetailRows();
        }
    }

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            if (_busy != value)
            {
                _busy = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
                GetTracksCommand.RaiseCanExecute();
                LoadFolderCommand.RaiseCanExecute();
                LookupCommand.RaiseCanExecute();
                UpdateCommand.RaiseCanExecute();
                OpenSettingsCommand.RaiseCanExecute();
                CancelCommand.RaiseCanExecute();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            }
        }
    }

    public string TrackCountText
    {
        get => _trackCountText;
        private set
        {
            if (_trackCountText != value)
            {
                _trackCountText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrackCountText)));
            }
        }
    }

    private string _trackCountText = "No tracks loaded";

    public string LyricsPreview
    {
        get => _lyricsPreview;
        private set
        {
            if (_lyricsPreview != value)
            {
                _lyricsPreview = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LyricsPreview)));
            }
        }
    }

    private string _lyricsPreview = string.Empty;

    public string LyricsStatus
    {
        get => _lyricsStatus;
        private set
        {
            if (_lyricsStatus != value)
            {
                _lyricsStatus = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LyricsStatus)));
            }
        }
    }

    private string _lyricsStatus = string.Empty;

    private int _lyricsFetchId;

    private readonly SemaphoreSlim _lyricsFetchLock = new(1, 1);

    /// <summary>Runs the per-track lyrics fetch serialized (LRCLib rate friendly) during the lookup.</summary>
    private async Task FetchLyricsInBackgroundAsync(TrackRowViewModel row)
    {
        await _lyricsFetchLock.WaitAsync().ConfigureAwait(true);
        try
        {
            await row.FetchLyricsAsync(_lyrics, CancellationToken.None).ConfigureAwait(true);
            if (ReferenceEquals(SelectedRow, row))
            {
                UpdateLyricsPanelFromRow();
            }
        }
        finally
        {
            _lyricsFetchLock.Release();
        }
    }

    private string ComposeLyricsStatus(TrackRowViewModel? row)
    {
        if (row is null)
        {
            return string.Empty;
        }

        var file = row.CurrentLyrics is { Length: > 0 } current
            ? $"File: has lyrics ({current.Length} chars)"
            : "File: no lyrics";

        return row.LyricsInfo is null ? file : $"{file} · {row.LyricsInfo}";
    }

    /// <summary>Shows the row's cached lyrics without hitting the network again.</summary>
    private void UpdateLyricsPanelFromRow()
    {
        var row = SelectedRow;
        LyricsPreview = row?.LyricsText ?? string.Empty;
        LyricsStatus = ComposeLyricsStatus(row);
    }

    /// <summary>
    /// Fetches (without writing) the LRCLib lyrics for the selected track so
    /// the user can preview what "Update" would write.
    /// </summary>
    public async Task RefreshLyricsPreviewAsync()
    {
        var row = SelectedRow;
        if (row is null)
        {
            LyricsPreview = string.Empty;
            LyricsStatus = string.Empty;
            return;
        }

        if (row.LyricsFetched)
        {
            UpdateLyricsPanelFromRow();
            return;
        }

        LyricsStatus = ComposeLyricsStatus(row);
        LyricsPreview = string.Empty;
        await FetchLyricsInBackgroundAsync(row).ConfigureAwait(true);
    }

    public int ProgressValue { get; private set; }

    public int ProgressMaximum { get; private set; }

    private void SetProgress(int value, int maximum)
    {
        ProgressValue = value;
        ProgressMaximum = maximum;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressValue)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressMaximum)));
    }

    // ------------------------------------------------------------------
    // Log
    // ------------------------------------------------------------------

    public void Log(string message, StatusKind severity = StatusKind.Neutral)
    {
        var line = new LogLineViewModel(message, severity);

        void Add()
        {
            _logLines.Add(line);
            StatusText = message;
        }

        // the lookup service logs from worker threads (ConfigureAwait(false))
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Add);
        }
        else
        {
            Add();
        }
    }

    // ------------------------------------------------------------------
    // 1. Get selected tracks
    // ------------------------------------------------------------------

    private async Task GetTracksAsync()
    {
        if (_busy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Reading selection from iTunes...";

            // COM must be touched on the UI (STA) thread.
            var tracks = _itunes.GetSelectedTracks();
            BindRows(tracks);
            await Task.CompletedTask;
        }
        catch (COMException ex)
        {
            Log($"iTunes COM error: {ex.Message}", StatusKind.Error);
            MessageBox.Show(
                "Could not talk to iTunes. Make sure iTunes for Windows is installed and running, then try again.",
                "iTunes Match Tagger", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            Log($"Unexpected error: {ex.Message}", StatusKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadFolderAsync()
    {
        if (_busy)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the folder that contains your m4a / mp3 files",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
            };
            var files = Directory.EnumerateFiles(dialog.FolderName, "*.*", options)
                .Where(static f => f.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)
                                  || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (files.Count == 0)
            {
                Log("No m4a or mp3 files found in the selected folder.", StatusKind.Warning);
                return;
            }

            BindRows(files.Select(static f => new FileTrack(f)));
        }
        catch (Exception ex)
        {
            Log($"Could not load the folder: {ex.Message}", StatusKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BindRows(IEnumerable<ITaggableTrack> tracks)
    {
        Rows.Clear();
        foreach (var track in tracks)
        {
            var row = new TrackRowViewModel(track)
            {
                LyricsWriteEnabled = _settings.IncludeLyrics,
            };
            row.PropertyChanged += OnRowChanged;
            Rows.Add(row);
        }

        ValidateRows();
        SelectedRow = null;
        TrackCountText = $"{Rows.Count} track(s) loaded";
        Log($"{Rows.Count} track(s) loaded.");
    }

    private void ValidateRows()
    {
        foreach (var row in Rows)
        {
            if (row.Track.Location is null)
            {
                row.SetStatus("Track is not downloaded!", StatusKind.Error);
            }
            else if (row.Track.TrackId == 0)
            {
                row.SetStatus(
                    row.Track is ComTrack
                        ? "Track is not matched (no embedded ID)"
                        : "No embedded ID - lookup will search by tags",
                    StatusKind.Warning);
            }
            else
            {
                row.SetStatus($"Matched - Track ID {row.Track.TrackId}", StatusKind.Success);
            }
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is TrackRowViewModel row && ReferenceEquals(SelectedRow, row))
        {
            RebuildDetailRows();
        }
    }

    // ------------------------------------------------------------------
    // 2. Lookup tracks
    // ------------------------------------------------------------------

    private bool CanLookup => !_busy && Rows.Count > 0;
    private bool CanUpdate => !_busy && Rows.Count > 0 && Options.Any(static o => o.Update);

    private async Task LookupAsync()
    {
        if (!EnsureReady())
        {
            return;
        }

        var countries = _settings.SelectedCountries;
        var (fallbackSources, errors) = BuildSourceChain();
        if (countries.Count == 0 && fallbackSources.Count == 0)
        {
            Log("No store countries configured and no tag sources ready.", StatusKind.Error);
            return;
        }

        IsBusy = true;
        SetProgress(0, Rows.Count);
        _cancellation = new CancellationTokenSource();
        Log($"Lookup started: {Rows.Count} track(s), {countries.Count} configured storefront(s), {fallbackSources.Count} fallback source(s).");

        var skippedSources = new HashSet<string>(StringComparer.Ordinal);
        var alwaysQueryAll = _settings.QueryAllSources;

        try
        {
            foreach (var row in Rows)
            {
                if (_cancellation.IsCancellationRequested)
                {
                    Log("Lookup cancelled.", StatusKind.Warning);
                    break;
                }

                var outcome = await TrackLookupService.LookupRowAsync(
                    row, _search, _artworkHttp, countries, fallbackSources, alwaysQueryAll,
                    skippedSources, Log, _cancellation.Token);

                ApplyLookupOutcome(row, outcome);
                SetProgress(ProgressValue + 1, ProgressMaximum);
                _ = FetchLyricsInBackgroundAsync(row); // cache the lyrics once, serialized
            }
        }
        catch (Exception ex)
        {
            Log($"Lookup failed: {ex.Message}", StatusKind.Error);
        }
        finally
        {
            IsBusy = false;
            StatusText = "Lookup finished";
        }
    }

    private (List<ITagSource> Sources, List<string> Errors) BuildSourceChain()
    {
        var errors = new List<string>();
        var sources = new List<ITagSource>();
        foreach (var source in _settings.Sources.Where(static s => s.Enabled))
        {
            var info = TagSourceCatalog.ById(source.Id);
            if (info is null)
            {
                continue;
            }

            try
            {
                var built = TagSourceFactory.Create(info, source.Token);
                if (built.RequiresCredentials && string.IsNullOrWhiteSpace(source.Token))
                {
                    errors.Add($"{info.DisplayName}: needs a token (Settings > Sources)");
                    continue;
                }

                sources.Add(built);
            }
            catch (Exception ex)
            {
                errors.Add($"{info.DisplayName}: {ex.Message}");
            }
        }

        return (sources, errors);
    }

    private void ApplyLookupOutcome(TrackRowViewModel row, TrackLookupService.LookupOutcome outcome)
    {
        var candidates = outcome.Candidates;
        if (candidates.Count > 0)
        {
            row.LookupSuccess = true;
            row.SetCandidates(candidates, outcome.AutoSelectIndex);

            foreach (var (candidateIndex, bytes) in outcome.ArtworkPreviews)
            {
                row.SetCandidateArtwork(candidateIndex, bytes);
            }

            row.SetStatus(TrackLookupService.BuildLookupStatus(row, outcome), StatusKind.Success);
            Log($"Found on {row.SourceBadge}: {row.File}");
        }
        else
        {
            row.LookupSuccess = false;
            row.ClearProposed();
            row.SetCandidates([], -1);
            row.SetStatus("Not found on Apple or the enabled tag sources", StatusKind.Error);
            Log($"Not found: {row.File} (Track ID {row.Track.TrackId})", StatusKind.Neutral);
        }
    }

    // ------------------------------------------------------------------
    // 3. Update tracks
    // ------------------------------------------------------------------

    private async Task UpdateAsync()
    {
        if (!EnsureReady())
        {
            return;
        }

        // always-write fields (hidden from the UI) join the option-driven set
        var alwaysWrite = TrackFields.All.Where(static f => f.AlwaysWrite)
            .Select(static f => (Field: f, Update: true, Overwrite: false, OverwriteValue: (string?)null));

        var active = Options.Where(static o => o.Update)
            .Select(static o => (Field: o.Field, o.Update, o.Overwrite, o.OverwriteValue))
            .Concat(alwaysWrite)
            .ToList();
        if (active.Count == 0)
        {
            Log("No update fields checked (Settings > Fields).", StatusKind.Error);
            return;
        }

        if (!Rows.Any(static r => !r.SkipUpdate))
        {
            Log("Every track is excluded from the update.", StatusKind.Warning);
            return;
        }

        IsBusy = true;
        SetProgress(0, Rows.Count);
        _cancellation = new CancellationTokenSource();
        Log("Update started.");

        var lyricsWrittenCount = 0;
        var lyricsMissedCount = 0;

        try
        {
            foreach (var row in Rows)
            {
                if (row.SkipUpdate)
                {
                    row.SetStatus("Excluded by the user's toggle", StatusKind.Neutral);
                    SetProgress(ProgressValue + 1, ProgressMaximum);
                    continue;
                }

                var writes = new List<KeyValuePair<TrackField, object?>>();
                string? artworkValue = null;
                foreach (var option in active)
                {
                    if (!row.IsFieldEnabled(option.Field.LookupMember))
                    {
                        continue; // unchecked in this track's comparison grid
                    }

                    var value = option.Overwrite && !string.IsNullOrEmpty(option.OverwriteValue)
                        ? option.OverwriteValue
                        : row.LookupSuccess
                            ? row.GetProposed(option.Field.LookupMember)
                            : null;

                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    if (option.Field == TrackFields.Artwork)
                    {
                        // handled separately: the value is a URL to download
                        // or a local image file path
                        artworkValue = value;
                        continue;
                    }

                    writes.Add(new KeyValuePair<TrackField, object?>(option.Field, value));
                    Log($"WRITE {option.Field.DisplayName} = '{value}' -> {row.File}", StatusKind.Neutral);
                }

                if (row.Track.Location is null)
                {
                    row.SetStatus("Track is not downloaded (iCloud only) - cannot update", StatusKind.Error);
                    Log($"SKIP update, no local file: {row.File}", StatusKind.Warning);
                    SetProgress(ProgressValue + 1, ProgressMaximum);
                    continue;
                }

                // heartbeat: is the COM backing still alive before writing?
                var preWrite = row.Track.ReadField(TrackFields.All[0]);
                Log($"WRITE pre-check '{TrackFields.All[0].DisplayName}' currently '{preWrite}' -> {row.File}", StatusKind.Neutral);

                var fieldsWritten = 0;
                try
                {
                    row.Track.WriteFields(writes);
                    fieldsWritten = writes.Count;
                }
                catch (Exception ex)
                {
                    Log($"Unable to write tags for '{row.File}': {ex.Message}", StatusKind.Error);
                }

                var artworkWritten = false;
                try
                {
                    if (artworkValue is not null)
                    {
                        if (File.Exists(artworkValue))
                        {
                            row.Track.WriteArtwork(await File.ReadAllBytesAsync(artworkValue).ConfigureAwait(true));
                            artworkWritten = true;
                        }
                        else if (artworkValue.StartsWith("http", StringComparison.OrdinalIgnoreCase) && row.LookupSuccess)
                        {
                            var bytes = await _artworkHttp.GetByteArrayAsync(
                                ITunesSearchClient.SizedArtworkUrl(artworkValue, 600), CancellationToken.None).ConfigureAwait(true);
                            row.Track.WriteArtwork(bytes);
                            artworkWritten = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Unable to write artwork for '{row.File}': {ex.Message}", StatusKind.Error);
                }

                var lyricsWritten = false;
                if (row.LyricsWriteEnabled)
                {
                    try
                    {
                        if (!row.LyricsFetched)
                        {
                            await FetchLyricsInBackgroundAsync(row).ConfigureAwait(true);
                        }

                        lyricsWritten = await row.WriteCachedLyricsAsync().ConfigureAwait(true);
                        if (lyricsWritten)
                        {
                            lyricsWrittenCount++;
                        }
                        else
                        {
                            lyricsMissedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        lyricsMissedCount++;
                        Log($"Unable to write lyrics for '{row.File}': {ex.Message}", StatusKind.Error);
                    }
                }

                var written = fieldsWritten + (artworkWritten ? 1 : 0) + (lyricsWritten ? 1 : 0);
                if (written > 0)
                {
                    row.SetStatus($"Updated ({written} change(s))", StatusKind.Success);
                }
                else if (!row.LookupSuccess)
                {
                    row.SetStatus("No lookup proposal for this track (run 2. Lookup)", StatusKind.Warning);
                }
                else if (writes.Count == 0)
                {
                    row.SetStatus("Tags already match the candidate", StatusKind.Success);
                }
                else
                {
                    row.SetStatus($"Nothing written - check the Log ({writes.Count} field(s) attempted)", StatusKind.Error);
                }

                // re-read the current values so the comparison shows the result
                row.RefreshCurrentValues();
                SetProgress(ProgressValue + 1, ProgressMaximum);
            }

            if (_settings.IncludeLyrics)
            {
                Log($"Lyrics summary: {lyricsWrittenCount} written, {lyricsMissedCount} without match/instrumental/without tags.");
            }

            Log("Update complete.", StatusKind.Success);
        }
        catch (OperationCanceledException)
        {
            Log("Update cancelled.", StatusKind.Warning);
        }
        catch (Exception ex)
        {
            Log($"Update failed: {ex.Message}", StatusKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Looks up lyrics on LRCLib using the proposed (or current) tags and
    /// writes them with synced LRC preferred over plain text.
    /// </summary>
    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v));

    // ------------------------------------------------------------------
    // Settings dialog
    // ------------------------------------------------------------------

    private void OpenSettings()
    {
        var snapshot = SettingsViewModel.FromSettings(_settings);
        var window = new SettingsWindow(snapshot);
        if (window.ShowDialog() == true)
        {
            snapshot.ApplyTo(_settings);
            _settings.Save();
            LoadOptionsFromSettings();
            foreach (var row in Rows)
            {
                row.LyricsWriteEnabled = _settings.IncludeLyrics;
            }

            Log("Settings saved.");
        }
    }

    private void LoadOptionsFromSettings()
    {
        Options = [.. TrackFields.All
            .Where(f => f.VisibleInOptions)
            .Select(f =>
            {
                var saved = _settings.Fields.GetValueOrDefault(f.LookupMember);
                return new OptionRowViewModel(f, saved?.Update ?? f.UpdateByDefault, saved?.Overwrite ?? false, saved?.OverwriteValue);
            })];
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Options)));
    }

    // ------------------------------------------------------------------
    // Detail panel
    // ------------------------------------------------------------------

    private void RebuildDetailRows()
    {
        DetailRows.Clear();
        var row = SelectedRow;
        if (row is null)
        {
            return;
        }

        var sourceIds = new List<string> { TagSources.ITunes };
        sourceIds.AddRange(_settings.Sources
            .Where(s => s.Enabled && s.Id != TagSources.ITunes)
            .Select(static s => s.Id)
            .Distinct());

        foreach (var field in TrackFields.All.Where(static f => f.VisibleInOptions))
        {
            DetailRows.Add(FieldComparisonRowViewModel.CreateValueRow(row, field, sourceIds));

            if (field == TrackFields.Artwork)
            {
                DetailRows.Add(FieldComparisonRowViewModel.CreateArtworkPreviewRow(row, sourceIds));
            }
        }
    }

    private void RefreshDetailRows()
    {
        foreach (var detail in DetailRows)
        {
            detail.Refresh();
        }
    }

    // ------------------------------------------------------------------
    // Plumbing
    // ------------------------------------------------------------------

    private bool EnsureReady()
    {
        if (_busy)
        {
            return false;
        }

        if (Rows.Count == 0)
        {
            Log("No tracks loaded. Get the iTunes selection or load a folder first.", StatusKind.Error);
            return false;
        }

        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>One line of the log panel.</summary>
public sealed record LogLineViewModel(string Message, StatusKind Severity)
{
    public string Time { get; } = DateTime.Now.ToString("HH:mm:ss");
}

/// <summary>Simple synchronous command.</summary>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : System.Windows.Input.ICommand
{
    private readonly Action _execute = execute;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecute() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
