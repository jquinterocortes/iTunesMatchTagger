using System.ComponentModel;
using System.Runtime.InteropServices;
using iTunesMatchTagger.Core.Fields;
using iTunesMatchTagger.Core.ITunes;
using iTunesMatchTagger.Core.Lookup;
using iTunesMatchTagger.Core.Settings;
using iTunesMatchTagger.Core.Tracks;

namespace iTunesMatchTagger.App;

/// <summary>
/// Main window. Ports the upstream three-step workflow
/// (get tracks - lookup - update) to .NET 10 with async lookups, and adds
/// the standalone "Load folder" mode that tags files without iTunes.
/// COM objects are only touched on the UI thread; lookups run in parallel
/// on background threads and report back through <see cref="Progress{T}"/>.
/// </summary>
public sealed class MainForm : Form
{
    private enum LogSeverity
    {
        Debug,
        Information,
        Warning,
        Error,
    }

    private sealed record LogEntry(string Message, LogSeverity Severity);

    private sealed record LookupOutcome(TrackRow Row, ITunesLookupResult? Result);

    private readonly ITunesComClient _itunes = new();
    private readonly ITunesSearchClient _search = new();
    private readonly List<TrackRow> _rows = [];
    private readonly BindingList<OptionRow> _options = [];
    private readonly IProgress<LogEntry> _logSink;
    private readonly IProgress<LookupOutcome> _lookupSink;
    private CancellationTokenSource? _cancellation;
    private bool _busy;

    private CheckedListBox _countries = new();
    private DataGridView _optionsGrid = new();
    private DataGridView _tracksGrid = new();
    private TextBox _log = new();
    private ProgressBar _progress = new();
    private CheckBox _showDebug = new();
    private Button _btnGet = new();
    private Button _btnLookup = new();
    private Button _btnUpdate = new();
    private Button _btnLoadFolder = new();
    private Button _btnInfo = new();

    public MainForm()
    {
        _logSink = new Progress<LogEntry>(AppendLog);
        _lookupSink = new Progress<LookupOutcome>(ApplyLookupOutcome);

        Text = $"iTunes Match Tagger v2 - v{typeof(MainForm).Assembly.GetName().Version?.ToString(3)}";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1080, 760);
        MinimumSize = new Size(940, 620);

        BuildLayout();

        foreach (var field in TrackFields.Visible)
        {
            _options.Add(new OptionRow(field));
        }

        LoadSettings();
    }

    private IReadOnlyList<string> CheckedCountries()
    {
        var selected = new List<string>();
        foreach (var item in _countries.CheckedItems)
        {
            if (item is string country)
            {
                selected.Add(country);
            }
        }

        return selected;
    }

    // ------------------------------------------------------------------
    // 1. Get selected tracks
    // ------------------------------------------------------------------

    private void BtnGet_Click(object? sender, EventArgs e)
    {
        if (!EnsureNotBusy())
        {
            return;
        }

        try
        {
            var tracks = _itunes.GetSelectedTracks();
            if (tracks.Count == 0)
            {
                Log("No tracks selected.", LogSeverity.Error);
                MessageBox.Show(this, "No tracks selected in iTunes.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            BindRows(tracks);
        }
        catch (COMException ex)
        {
            Log($"iTunes COM error: {ex.Message}", LogSeverity.Error);
            MessageBox.Show(this,
                "Could not talk to iTunes. Make sure iTunes for Windows is installed and running, then try again.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ------------------------------------------------------------------
    // Standalone: load audio files from a folder (no iTunes involved)
    // ------------------------------------------------------------------

    private void BtnLoadFolder_Click(object? sender, EventArgs e)
    {
        if (!EnsureNotBusy())
        {
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the folder that contains your m4a / mp3 files.",
            ShowNewFolderButton = false,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
        };
        var files = Directory.EnumerateFiles(dialog.SelectedPath, "*.*", options)
            .Where(f => f.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (files.Count == 0)
        {
            Log("No m4a or mp3 files found in the selected folder.", LogSeverity.Warning);
            MessageBox.Show(this, "No m4a or mp3 files found in the selected folder.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        BindRows(files.Select(f => new FileTrack(f)));
    }

    private void BindRows(IEnumerable<ITaggableTrack> tracks)
    {
        _rows.Clear();
        _rows.AddRange(tracks.Select(t => new TrackRow(t)));

        _tracksGrid.DataSource = _rows;
        _tracksGrid.Refresh();
        ValidateRows();

        Log($"{_rows.Count} track(s) loaded.");
    }

    private void ValidateRows()
    {
        foreach (DataGridViewRow gridRow in _tracksGrid.Rows)
        {
            if (gridRow.DataBoundItem is not TrackRow row)
            {
                continue;
            }

            gridRow.ErrorText = row.Track.Location is null
                ? "Track is not downloaded!"
                : row.Track.TrackId == 0
                    ? row.Track is ComTrack
                        ? "Track is not matched!"
                        : "No embedded Track ID - lookup will search by current tags"
                    : string.Empty;

            if (gridRow.ErrorText.Length > 0)
            {
                Log($"{gridRow.ErrorText} {row.File}", LogSeverity.Debug);
            }
        }
    }

    // ------------------------------------------------------------------
    // 2. Lookup tracks
    // ------------------------------------------------------------------

    private async void BtnLookup_Click(object? sender, EventArgs e)
    {
        if (!EnsureNotBusy())
        {
            return;
        }

        var countries = CheckedCountries();
        if (countries.Count == 0)
        {
            Log("No store countries selected.", LogSeverity.Error);
            MessageBox.Show(this, "No store countries selected.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_rows.Count == 0)
        {
            Log("No tracks loaded.", LogSeverity.Error);
            MessageBox.Show(this, "No tracks loaded. Click \"1. Get selected tracks\" or \"Load folder...\" first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_options.Any(o => o.Update))
        {
            Log("No update fields checked.", LogSeverity.Error);
            MessageBox.Show(this, "No update fields checked.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        _progress.Value = 0;
        _progress.Maximum = _rows.Count;
        Log($"Lookup started: {_rows.Count} track(s) across {countries.Count} countr(ies).");

        try
        {
            await Parallel.ForEachAsync(
                _rows,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = _cancellation!.Token },
                async (row, cancellationToken) =>
                {
                    var found = await LookupRowAsync(row, countries, cancellationToken).ConfigureAwait(false);
                    _lookupSink.Report(new LookupOutcome(row, found));
                }).ConfigureAwait(true);

            Log($"{_rows.Count(static r => r.LookupSuccess)} of {_rows.Count} track(s) found.");
        }
        catch (OperationCanceledException)
        {
            Log("Lookup cancelled.", LogSeverity.Warning);
        }
        catch (HttpRequestException ex)
        {
            Log($"Lookup failed: {ex.Message}", LogSeverity.Error);
        }
        finally
        {
            SetBusy(false);
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private async Task<ITunesLookupResult?> LookupRowAsync(TrackRow row, IReadOnlyList<string> countries, CancellationToken cancellationToken)
    {
        if (row.Track.TrackId > 0)
        {
            foreach (var country in countries)
            {
                var result = await _search.LookupTrackAsync(row.Track.TrackId, country, cancellationToken).ConfigureAwait(false);
                if (result is not null)
                {
                    LogDebug($"Track ID {row.Track.TrackId} found in {country}: {row.File}");
                    return result;
                }
            }

            return null;
        }

        // Standalone fallback for files without an embedded ID: search by
        // the tags currently on the file.
        var term = $"{row.ArtistName} {row.TrackName}".Trim();
        if (term.Length == 0)
        {
            Log($"No Track ID and no tags to search for: {row.File}", LogSeverity.Warning);
            return null;
        }

        foreach (var country in countries)
        {
            var results = await _search.SearchAsync(term, country, cancellationToken).ConfigureAwait(false);
            var first = results.FirstOrDefault();
            if (first is not null)
            {
                LogDebug($"Term '{term}' found in {country}: {row.File}");
                return first;
            }
        }

        return null;
    }

    /// <summary>Runs on the UI thread (posted through <see cref="Progress{T}"/>).</summary>
    private void ApplyLookupOutcome(LookupOutcome outcome)
    {
        var (row, result) = outcome;
        row.LookupSuccess = result is not null;

        if (result is not null)
        {
            foreach (var option in _options.Where(static o => o.Update))
            {
                var value = option.Field.GetFromLookup(result);
                if (value is not null)
                {
                    row.SetValue(option.Field.LookupMember, value.ToString());
                }
            }
        }

        _progress.PerformStep();
        _tracksGrid.Invalidate();
    }

    // ------------------------------------------------------------------
    // 3. Update tracks
    // ------------------------------------------------------------------

    private void BtnUpdate_Click(object? sender, EventArgs e)
    {
        if (!EnsureNotBusy())
        {
            return;
        }

        var active = _options.Where(static o => o.Update).ToList();
        if (active.Count == 0)
        {
            Log("No update fields checked.", LogSeverity.Error);
            MessageBox.Show(this, "No update fields checked.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_rows.Count == 0)
        {
            Log("No tracks loaded.", LogSeverity.Error);
            MessageBox.Show(this, "No tracks loaded.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        _progress.Value = 0;
        _progress.Maximum = _rows.Count;
        Log("Update started.");

        try
        {
            foreach (var row in _rows)
            {
                var writes = new List<KeyValuePair<TrackField, object?>>();
                foreach (var option in active)
                {
                    var value = option.Overwrite && !string.IsNullOrEmpty(option.OverwriteValue)
                        ? option.OverwriteValue
                        : row.LookupSuccess
                            ? row.GetValue(option.Field.LookupMember)
                            : null;

                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    writes.Add(new KeyValuePair<TrackField, object?>(option.Field, value));
                    LogDebug($"{option.Field.DisplayName} = '{value}' -> {row.File}");
                }

                try
                {
                    row.Track.WriteFields(writes);
                }
                catch (Exception ex)
                {
                    Log($"Unable to update '{row.File}': {ex.Message}", LogSeverity.Error);
                }

                // re-read the current values so the grid shows the result
                foreach (var field in TrackFields.All)
                {
                    row.SetValue(field.LookupMember, row.Track.ReadField(field)?.ToString());
                }

                _progress.PerformStep();
            }

            Log("Update complete.");
            MessageBox.Show(this, "Update complete!", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ------------------------------------------------------------------
    // UI plumbing
    // ------------------------------------------------------------------

    private bool EnsureNotBusy()
    {
        if (_busy)
        {
            MessageBox.Show(this, "Another operation is running.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        return !_busy;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UseWaitCursor = busy;
        _btnGet.Enabled = _btnLoadFolder.Enabled = !busy;
        _btnLookup.Enabled = _btnUpdate.Enabled = !busy;
    }

    private void Log(string message, LogSeverity severity = LogSeverity.Information) => _logSink.Report(new LogEntry(message, severity));

    private void LogDebug(string message) => Log(message, LogSeverity.Debug);

    private void AppendLog(LogEntry entry)
    {
        if (entry.Severity == LogSeverity.Debug && !_showDebug.Checked)
        {
            return;
        }

        var prefix = entry.Severity switch
        {
            LogSeverity.Error => "Error: ",
            LogSeverity.Warning => "Warning: ",
            _ => string.Empty,
        };

        _log.AppendText(prefix + entry.Message + Environment.NewLine);
    }

    private void BtnInfo_Click(object? sender, EventArgs e)
    {
        MessageBox.Show(this,
            "iTunes Match Tagger v2\n\n" +
            "Repairs the metadata of iTunes Match tracks via the iTunes\n" +
            "Search API, and tags m4a/mp3 files directly in standalone mode.\n\n" +
            "Based on iTunes Match Tagger by Martin Pietschmann (schirkan), 2012.\n" +
            "Licensed under the GNU General Public License v3.\n\n" +
            "https://github.com/jquinterocortes/iTunesMatchTagger",
            "About",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void BtnSelectAllCountries_Click(object? sender, EventArgs e)
    {
        for (var i = 0; i < _countries.Items.Count; i++)
        {
            _countries.SetItemChecked(i, true);
        }
    }

    private void BtnClearCountries_Click(object? sender, EventArgs e)
    {
        for (var i = 0; i < _countries.Items.Count; i++)
        {
            _countries.SetItemChecked(i, false);
        }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 240));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        root.Controls.Add(BuildTopPanel(), 0, 0);
        root.Controls.Add(BuildTracksPanel(), 0, 1);
        root.Controls.Add(BuildLogPanel(), 0, 2);
        root.Controls.Add(BuildBottomBar(), 0, 3);
    }

    private Control BuildTopPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        var countriesGroup = new GroupBox
        {
            Text = "iTunes Store countries",
            Dock = DockStyle.Fill,
        };
        _countries = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false,
        };
        foreach (var country in StoreCountries.All)
        {
            _countries.Items.Add(country, StoreCountries.DefaultSelected.Contains(country));
        }

        var countryButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            Height = 32,
        };
        countryButtons.Controls.Add(new Button { Text = "Select all", AutoSize = true });
        ((Button)countryButtons.Controls[0]).Click += BtnSelectAllCountries_Click;
        var clearButton = new Button { Text = "Clear", AutoSize = true };
        clearButton.Click += BtnClearCountries_Click;
        countryButtons.Controls.Add(clearButton);

        countriesGroup.Controls.Add(_countries);
        countriesGroup.Controls.Add(countryButtons);

        var optionsGroup = new GroupBox
        {
            Text = "Fields to update",
            Dock = DockStyle.Fill,
        };
        _optionsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            BackgroundColor = SystemColors.Window,
        };
        _optionsGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Update",
            HeaderText = "Update",
            DataPropertyName = nameof(OptionRow.Update),
            Width = 56,
        });
        _optionsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Field",
            HeaderText = "Field",
            DataPropertyName = nameof(OptionRow.FieldName),
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        _optionsGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Overwrite",
            HeaderText = "Overwrite",
            DataPropertyName = nameof(OptionRow.Overwrite),
            Width = 68,
        });
        _optionsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "OverwriteValue",
            HeaderText = "Value",
            DataPropertyName = nameof(OptionRow.OverwriteValue),
            Width = 90,
        });
        _optionsGrid.DataSource = _options;
        optionsGroup.Controls.Add(_optionsGrid);

        panel.Controls.Add(countriesGroup, 0, 0);
        panel.Controls.Add(optionsGroup, 1, 0);
        return panel;
    }

    private Control BuildTracksPanel()
    {
        var group = new GroupBox
        {
            Text = "Tracks",
            Dock = DockStyle.Fill,
        };

        _tracksGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
            BackgroundColor = SystemColors.Window,
        };
        foreach (var field in TrackFields.All)
        {
            _tracksGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = field.LookupMember,
                HeaderText = field.DisplayName,
                DataPropertyName = TrackRow.PropertyByLookupMember[field.LookupMember],
            });
        }

        _tracksGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "TrackId",
            HeaderText = "Track ID",
            DataPropertyName = nameof(TrackRow.TrackId),
        });

        group.Controls.Add(_tracksGrid);
        return group;
    }

    private Control BuildLogPanel()
    {
        var group = new GroupBox
        {
            Text = "Log",
            Dock = DockStyle.Fill,
        };
        _log = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 8.25f),
        };
        group.Controls.Add(_log);
        return group;
    }

    private Control BuildBottomBar()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _progress = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Height = 22,
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        _btnGet = new Button { Text = "1. Get selected tracks", AutoSize = true };
        _btnGet.Click += BtnGet_Click;
        _btnLookup = new Button { Text = "2. Lookup tracks", AutoSize = true };
        _btnLookup.Click += BtnLookup_Click;
        _btnUpdate = new Button { Text = "3. Update tracks", AutoSize = true };
        _btnUpdate.Click += BtnUpdate_Click;
        _btnLoadFolder = new Button { Text = "Load folder...", AutoSize = true };
        _btnLoadFolder.Click += BtnLoadFolder_Click;
        _btnInfo = new Button { Text = "Info", AutoSize = true };
        _btnInfo.Click += BtnInfo_Click;
        _showDebug = new CheckBox { Text = "Show debug", AutoSize = true, Checked = false, Padding = new Padding(8, 6, 0, 0) };

        buttons.Controls.Add(_btnGet);
        buttons.Controls.Add(_btnLookup);
        buttons.Controls.Add(_btnUpdate);
        buttons.Controls.Add(_btnLoadFolder);
        buttons.Controls.Add(_btnInfo);
        buttons.Controls.Add(_showDebug);

        panel.Controls.Add(_progress, 0, 0);
        panel.Controls.Add(buttons, 1, 0);
        return panel;
    }

    // ------------------------------------------------------------------
    // Settings
    // ------------------------------------------------------------------

    private void LoadSettings()
    {
        var settings = AppSettings.Load();

        for (var i = 0; i < StoreCountries.All.Count; i++)
        {
            _countries.SetItemChecked(i, settings.SelectedCountries.Contains(StoreCountries.All[i]));
        }

        foreach (var option in _options)
        {
            if (settings.Fields.TryGetValue(option.Field.LookupMember, out var saved))
            {
                option.Update = saved.Update;
                option.Overwrite = saved.Overwrite;
                option.OverwriteValue = saved.OverwriteValue;
            }
        }
    }

    private void SaveSettings()
    {
        var settings = new AppSettings
        {
            SelectedCountries = [.. CheckedCountries()],
        };

        foreach (var option in _options)
        {
            settings.Fields[option.Field.LookupMember] = new AppSettings.FieldOptions
            {
                Update = option.Update,
                Overwrite = option.Overwrite,
                OverwriteValue = option.OverwriteValue,
            };
        }

        settings.Save();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            _cancellation?.Cancel();
        }

        _search.Dispose();
        _itunes.Dispose();
        SaveSettings();
        base.OnFormClosing(e);
    }
}
