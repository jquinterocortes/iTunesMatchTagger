using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using iTunesMatchTagger.Core.Fields;
using iTunesMatchTagger.Core.ITunes;
using iTunesMatchTagger.Core.Lookup;
using iTunesMatchTagger.Core.Settings;
using iTunesMatchTagger.Core.Tracks;

namespace iTunesMatchTagger.App;

/// <summary>
/// Main window: a master-detail tag validator.
/// Left: track list with artwork thumbnails and per-track status.
/// Right: the selected track's artwork (current vs from Apple) and a
/// field-by-field comparison (current vs proposed) with differences
/// highlighted, so every change can be reviewed before "3. Update tracks".
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

    private sealed record LookupOutcome(TrackRow Row, ITunesLookupResult? Result, string FoundIn, bool FoundViaSearch, byte[]? ArtworkBytes);

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
    private ListBox _trackList = new();
    private SplitContainer _splitter = new();
    private DataGridView _detailGrid = new();
    private PictureBox _artworkCurrentPic = new();
    private PictureBox _artworkNewPic = new();
    private Label _statusLabel = new();
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
        ClientSize = new Size(1380, 900);
        MinimumSize = new Size(1160, 720);

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
        catch (Exception ex)
        {
            Log($"Unexpected error: {ex.Message}", LogSeverity.Error);
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
        foreach (var row in _rows)
        {
            row.PropertyChanged -= OnRowChanged;
            row.DisposeImages();
        }

        _rows.Clear();
        foreach (var track in tracks)
        {
            var row = new TrackRow(track);
            row.PropertyChanged += OnRowChanged;
            _rows.Add(row);
        }

        _trackList.BeginUpdate();
        _trackList.Items.Clear();
        foreach (var row in _rows)
        {
            _trackList.Items.Add(row);
        }
        _trackList.EndUpdate();

        ValidateRows();
        FillDetail(null);

        Log($"{_rows.Count} track(s) loaded.");
        foreach (var row in _rows)
        {
            LogDebug($"{row.File} -> Track ID {row.Track.TrackId}{(row.Track.TrackId > 0 ? string.Empty : " (none)")}");
        }
    }

    private void ValidateRows()
    {
        foreach (var row in _rows)
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
                row.SetStatus($"Matched - Track ID {row.Track.TrackId}", StatusKind.Neutral);
            }
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        _trackList.Invalidate();
        if (sender is TrackRow row && ReferenceEquals(SelectedRow(), row))
        {
            FillDetail(row);
        }
    }

    private TrackRow? SelectedRow() =>
        _trackList.SelectedIndex >= 0 && _trackList.SelectedIndex < _rows.Count
            ? _rows[_trackList.SelectedIndex]
            : null;

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

        if (!_options.Any(static o => o.Update))
        {
            Log("No update fields checked.", LogSeverity.Error);
            MessageBox.Show(this, "No update fields checked.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        _cancellation = new CancellationTokenSource();
        _progress.Value = 0;
        _progress.Maximum = _rows.Count;
        Log($"Lookup started: {_rows.Count} track(s) across {countries.Count} countr(ies).");

        try
        {
            await Parallel.ForEachAsync(
                _rows,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = _cancellation.Token },
                async (row, cancellationToken) =>
                {
                    var outcome = await LookupRowAsync(row, countries, cancellationToken).ConfigureAwait(false);
                    _lookupSink.Report(outcome);
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
        catch (Exception ex)
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

    private async Task<LookupOutcome> LookupRowAsync(TrackRow row, IReadOnlyList<string> countries, CancellationToken cancellationToken)
    {
        ITunesLookupResult? result = null;
        var foundIn = string.Empty;
        var viaSearch = false;

        if (row.Track.TrackId > 0)
        {
            foreach (var country in countries)
            {
                var found = await _search.LookupTrackAsync(row.Track.TrackId, country, cancellationToken).ConfigureAwait(false);
                if (found is not null)
                {
                    LogDebug($"Track ID {row.Track.TrackId} found in {country}: {row.File}");
                    result = found;
                    foundIn = country;
                    break;
                }
            }

            if (result is null)
            {
                // The embedded ID is dead in every selected storefront (Apple
                // delists albums; the file keeps the old ID). Fall back to
                // searching by the current tags - re-released albums come back
                // under a new ID that an ID lookup would never find.
                Log($"Track ID {row.Track.TrackId} not found in any selected country, trying search by current tags: {row.File}", LogSeverity.Information);
            }
        }
        else
        {
            Log($"No embedded Track ID, searching by current tags: {row.File}", LogSeverity.Information);
        }

        if (result is null)
        {
            var term = $"{row.GetCurrent("artistName")} {row.GetCurrent("trackName")}".Trim();
            if (term.Length == 0)
            {
                Log($"No tags to search for: {row.File}", LogSeverity.Warning);
            }
            else
            {
                foreach (var country in countries)
                {
                    var results = await _search.SearchAsync(term, country, cancellationToken).ConfigureAwait(false);
                    var first = results.FirstOrDefault(static r => r.Kind is null or "song");
                    if (first is not null)
                    {
                        LogDebug($"Term '{term}' found in {country}: {row.File} (catalog Track ID {first.TrackId})");
                        result = first;
                        foundIn = country;
                        viaSearch = true;
                        break;
                    }
                }
            }
        }

        byte[]? artworkBytes = null;
        if (result?.ArtworkUrl100 is not null)
        {
            try
            {
                artworkBytes = await _search.DownloadArtworkAsync(result.ArtworkUrl100, 300, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                LogDebug($"Artwork preview download failed: {ex.Message}");
            }
        }

        return new LookupOutcome(row, result, foundIn, viaSearch, artworkBytes);
    }

    /// <summary>Runs on the UI thread (posted through <see cref="Progress{T}"/>).</summary>
    private void ApplyLookupOutcome(LookupOutcome outcome)
    {
        var row = outcome.Row;
        _progress.PerformStep();

        if (outcome.Result is not null)
        {
            var result = outcome.Result;
            row.LookupSuccess = true;

            foreach (var option in _options.Where(static o => o.Update))
            {
                var value = option.Field.GetFromLookup(result);
                row.SetProposed(option.Field.LookupMember, value?.ToString());
            }

            row.SetArtworkImage(ToImage(outcome.ArtworkBytes));

            row.SetStatus(
                outcome.FoundViaSearch
                    ? $"Found via search in {outcome.FoundIn} (catalog ID {result.TrackId})"
                    : $"Found in {outcome.FoundIn}",
                StatusKind.Success);
        }
        else
        {
            row.LookupSuccess = false;
            row.ClearProposed();
            row.SetArtworkImage(null);
            row.SetStatus("Not found in any selected country", StatusKind.Error);
            Log($"Not found: {row.File} (Track ID {row.Track.TrackId})", LogSeverity.Information);
        }

        _trackList.Invalidate();
        if (ReferenceEquals(SelectedRow(), row))
        {
            FillDetail(row);
        }
    }

    // ------------------------------------------------------------------
    // 3. Update tracks
    // ------------------------------------------------------------------

    private async void BtnUpdate_Click(object? sender, EventArgs e)
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
                string? artworkValue = null;
                foreach (var option in active)
                {
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
                    LogDebug($"{option.Field.DisplayName} = '{value}' -> {row.File}");
                }

                var fieldsWritten = 0;
                try
                {
                    row.Track.WriteFields(writes);
                    fieldsWritten = writes.Count;
                }
                catch (Exception ex)
                {
                    Log($"Unable to write tags for '{row.File}': {ex.Message}", LogSeverity.Error);
                }

                var artworkWritten = false;
                try
                {
                    if (artworkValue is not null)
                    {
                        if (File.Exists(artworkValue))
                        {
                            row.Track.WriteArtwork(await File.ReadAllBytesAsync(artworkValue).ConfigureAwait(true));
                            LogDebug($"Artwork from '{artworkValue}' -> {row.File}");
                            artworkWritten = true;
                        }
                        else if (artworkValue.StartsWith("http", StringComparison.OrdinalIgnoreCase) && row.LookupSuccess)
                        {
                            var bytes = await _search.DownloadArtworkAsync(artworkValue).ConfigureAwait(true);
                            row.Track.WriteArtwork(bytes);
                            LogDebug($"Artwork written ({bytes.Length} bytes) -> {row.File}");
                            artworkWritten = true;
                        }
                        else
                        {
                            LogDebug($"No artwork available for {row.File} (run a lookup first)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Unable to write artwork for '{row.File}': {ex.Message}", LogSeverity.Error);
                }

                var written = fieldsWritten + (artworkWritten ? 1 : 0);
                row.SetStatus(
                    written > 0 ? $"Updated ({written} field(s))" : "Nothing to update",
                    written > 0 ? StatusKind.Success : StatusKind.Warning);

                // re-read the current values so the comparison shows the result
                foreach (var field in TrackFields.All)
                {
                    row.SetCurrent(field.LookupMember, row.Track.ReadField(field)?.ToString());
                }

                _progress.PerformStep();
            }

            Log("Update complete.");
            MessageBox.Show(this, "Update complete!", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log($"Update failed: {ex.Message}", LogSeverity.Error);
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

    private void TrackList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var row = SelectedRow();
        if (row is null)
        {
            FillDetail(null);
            return;
        }

        LoadCurrentArtwork(row);
        FillDetail(row);
    }

    private void LoadCurrentArtwork(TrackRow row)
    {
        var bytes = row.Track.Location is null ? null : ArtworkReader.ReadFrontCover(row.Track.Location);
        row.SetCurrentArtworkImage(ToImage(bytes));
    }

    private void FillDetail(TrackRow? row)
    {
        _detailGrid.Rows.Clear();

        if (row is null)
        {
            _artworkCurrentPic.Image = null;
            _artworkNewPic.Image = null;
            _statusLabel.Text = "Select a track to review its tags.";
            _statusLabel.ForeColor = SystemColors.ControlText;
            return;
        }

        foreach (var field in TrackFields.All)
        {
            if (field.LookupMember == "Filename" || field == TrackFields.Artwork)
            {
                continue; // the list shows the file; artwork is shown as pictures
            }

            var current = row.GetCurrent(field.LookupMember);
            var proposed = row.GetProposed(field.LookupMember);
            var index = _detailGrid.Rows.Add(field.DisplayName, current ?? string.Empty, proposed ?? string.Empty);

            if (proposed is not null)
            {
                var changed = !string.Equals(current, proposed, StringComparison.Ordinal);
                _detailGrid.Rows[index].DefaultCellStyle.BackColor = changed
                    ? Color.FromArgb(255, 246, 220) // will change - amber
                    : Color.FromArgb(232, 245, 233); // identical - light green
            }
        }

        _artworkCurrentPic.Image = row.CurrentArtworkImage;
        _artworkNewPic.Image = row.ArtworkImage;

        _statusLabel.Text = row.StatusMessage.Length == 0
            ? "No lookup yet - click \"2. Lookup tracks\"."
            : row.StatusMessage;
        _statusLabel.ForeColor = row.StatusSeverity switch
        {
            StatusKind.Success => Color.FromArgb(0, 128, 0),
            StatusKind.Warning => Color.FromArgb(176, 96, 0),
            StatusKind.Error => Color.FromArgb(192, 0, 0),
            _ => SystemColors.ControlText,
        };
    }

    private bool WillChangeSomething(TrackRow row)
    {
        foreach (var option in _options.Where(static o => o.Update))
        {
            if (option.Overwrite && !string.IsNullOrEmpty(option.OverwriteValue))
            {
                return true;
            }

            if (row.LookupSuccess &&
                !string.Equals(row.GetCurrent(option.Field.LookupMember), row.GetProposed(option.Field.LookupMember), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void TrackList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _rows.Count)
        {
            return;
        }

        var row = _rows[e.Index];
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var backColor = selected ? SystemColors.Highlight : SystemColors.Window;
        var foreColor = selected ? SystemColors.HighlightText : SystemColors.WindowText;

        using (var backBrush = new SolidBrush(backColor))
        {
            e.Graphics.FillRectangle(backBrush, e.Bounds);
        }

        var thumbRect = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top + 6, 44, 44);
        if (row.ArtworkImage is not null)
        {
            e.Graphics.DrawImage(row.ArtworkImage, thumbRect);
        }
        else
        {
            using (var placeholderBrush = new SolidBrush(selected ? Color.FromArgb(60, SystemColors.ControlDark) : Color.FromArgb(240, 240, 240)))
            {
                e.Graphics.FillRectangle(placeholderBrush, thumbRect);
            }

            using var placeholderFont = new Font("Segoe UI", 7f);
            TextRenderer.DrawText(e.Graphics, "no art", placeholderFont, thumbRect,
                selected ? foreColor : SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        using (var borderPen = new Pen(Color.FromArgb(210, 210, 210)))
        {
            e.Graphics.DrawRectangle(borderPen, thumbRect);
        }

        var textLeft = thumbRect.Right + 8;
        var textWidth = Math.Max(10, e.Bounds.Width - textLeft - 14);
        using (var nameFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
        {
            var title = row.GetCurrent("trackName");
            TextRenderer.DrawText(e.Graphics,
                string.IsNullOrEmpty(title) ? "(unknown title)" : title,
                nameFont, new Rectangle(textLeft, e.Bounds.Top + 3, textWidth, 18), foreColor, TextFormatFlags.EndEllipsis);
        }

        using (var subFont = new Font("Segoe UI", 8.25f))
        {
            var subtitle = string.Join(" - ",
                new[] { row.GetCurrent("artistName"), row.GetCurrent("collectionName") }
                    .Where(static s => !string.IsNullOrEmpty(s)));
            TextRenderer.DrawText(e.Graphics, subtitle, subFont,
                new Rectangle(textLeft, e.Bounds.Top + 21, textWidth, 16), foreColor, TextFormatFlags.EndEllipsis);

            var statusColor = row.StatusSeverity switch
            {
                StatusKind.Success => Color.FromArgb(0, 140, 0),
                StatusKind.Warning => Color.FromArgb(190, 110, 0),
                StatusKind.Error => Color.FromArgb(200, 0, 0),
                _ => selected ? foreColor : SystemColors.GrayText,
            };
            TextRenderer.DrawText(e.Graphics, row.StatusMessage, subFont,
                new Rectangle(textLeft, e.Bounds.Top + 38, textWidth, 16),
                selected ? foreColor : statusColor, TextFormatFlags.EndEllipsis);
        }

        if (WillChangeSomething(row))
        {
            using var dotBrush = new SolidBrush(Color.OrangeRed);
            e.Graphics.FillEllipse(dotBrush, e.Bounds.Right - 16, e.Bounds.Top + 9, 9, 9);
        }

        if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
        {
            e.DrawFocusRectangle();
        }
    }

    /// <summary>
    /// Creates an Image from image bytes. On success the underlying stream
    /// intentionally stays referenced by the Image (GDI+ requirement).
    /// </summary>
    private static Image? ToImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        var stream = new MemoryStream(bytes);
        try
        {
            return Image.FromStream(stream);
        }
        catch (ArgumentException)
        {
            stream.Dispose();
            return null;
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
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
            MultiColumn = true, // 134 storefronts - columns avoid long scrolling
            ColumnWidth = 130,
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
            Width = 110,
        });
        _optionsGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Overwrite",
            HeaderText = "Overwrite",
            DataPropertyName = nameof(OptionRow.Overwrite),
            Width = 72,
        });
        _optionsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "OverwriteValue",
            HeaderText = "Value",
            DataPropertyName = nameof(OptionRow.OverwriteValue),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        _optionsGrid.DataSource = _options;
        optionsGroup.Controls.Add(_optionsGrid);

        panel.Controls.Add(countriesGroup, 0, 0);
        panel.Controls.Add(optionsGroup, 1, 0);
        return panel;
    }

    private Control BuildTracksPanel()
    {
        _splitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.Panel1, // keep the list width when resizing
        };

        _trackList = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 56,
            IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle,
        };
        typeof(ListBox)
            .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(_trackList, true);
        _trackList.DrawItem += TrackList_DrawItem;
        _trackList.SelectedIndexChanged += TrackList_SelectedIndexChanged;
        _splitter.Panel1.Controls.Add(_trackList);

        var detail = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
        };
        detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        detail.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detail.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215));
        detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            AutoEllipsis = true,
            Padding = new Padding(4, 0, 0, 0),
        };
        detail.Controls.Add(_statusLabel, 0, 0);
        detail.SetColumnSpan(_statusLabel, 2);

        // artwork previews stacked vertically; the comparison grid sits
        // horizontally next to them and uses the full remaining width
        var artworkColumn = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        artworkColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        artworkColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var currentGroup = new GroupBox
        {
            Text = "Current artwork",
            Dock = DockStyle.Fill,
        };
        _artworkCurrentPic = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
        };
        currentGroup.Controls.Add(_artworkCurrentPic);

        var newGroup = new GroupBox
        {
            Text = "New (from Apple)",
            Dock = DockStyle.Fill,
        };
        _artworkNewPic = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
        };
        newGroup.Controls.Add(_artworkNewPic);

        artworkColumn.Controls.Add(currentGroup, 0, 0);
        artworkColumn.Controls.Add(newGroup, 0, 1);

        detail.Controls.Add(artworkColumn, 0, 1);
        detail.Controls.Add(BuildCompareGrid(), 1, 1);

        _splitter.Panel2.Controls.Add(detail);

        return _splitter;
    }

    protected override void OnShown(EventArgs e)
    {
        // SplitterDistance is only reliable once the form has final layout
        // (setting it in the constructor silently failed)
        base.OnShown(e);
        try
        {
            _splitter.SplitterDistance = 340;
        }
        catch (InvalidOperationException)
        {
            // keep the default split; user can drag
        }
    }

    private Control BuildCompareGrid()
    {
        var group = new GroupBox
        {
            Text = "Tag comparison (current vs from Apple)",
            Dock = DockStyle.Fill,
        };
        _detailGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = SystemColors.Window,
        };
        _detailGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Field",
            HeaderText = "Field",
            Width = 120,
        });
        _detailGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Current",
            HeaderText = "Current",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 50,
        });
        _detailGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Proposed",
            HeaderText = "From Apple",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 50,
        });
        group.Controls.Add(_detailGrid);
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
        // One plain FlowLayoutPanel: every control keeps its preferred size.
        // (Docking a FlowLayoutPanel inside an auto-sized TableLayoutPanel
        // column breaks the preferred-size measurement and clipped buttons.)
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        _progress = new ProgressBar
        {
            Width = 200,
            Height = 22,
            Margin = new Padding(0, 13, 12, 13),
        };

        var buttonMargin = new Padding(0, 10, 8, 10);
        _btnGet = new Button { Text = "1. Get selected tracks", AutoSize = true, Margin = buttonMargin };
        _btnGet.Click += BtnGet_Click;
        _btnLookup = new Button { Text = "2. Lookup tracks", AutoSize = true, Margin = buttonMargin };
        _btnLookup.Click += BtnLookup_Click;
        _btnUpdate = new Button { Text = "3. Update tracks", AutoSize = true, Margin = buttonMargin };
        _btnUpdate.Click += BtnUpdate_Click;
        _btnLoadFolder = new Button { Text = "Load folder...", AutoSize = true, Margin = buttonMargin };
        _btnLoadFolder.Click += BtnLoadFolder_Click;
        _btnInfo = new Button { Text = "Info", AutoSize = true, Margin = buttonMargin };
        _btnInfo.Click += BtnInfo_Click;
        _showDebug = new CheckBox { Text = "Show debug", AutoSize = true, Margin = new Padding(12, 12, 0, 0) };

        bar.Controls.Add(_progress);
        bar.Controls.Add(_btnGet);
        bar.Controls.Add(_btnLookup);
        bar.Controls.Add(_btnUpdate);
        bar.Controls.Add(_btnLoadFolder);
        bar.Controls.Add(_btnInfo);
        bar.Controls.Add(_showDebug);
        return bar;
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

        foreach (var row in _rows)
        {
            row.PropertyChanged -= OnRowChanged;
            row.DisposeImages();
        }

        _search.Dispose();
        _itunes.Dispose();
        SaveSettings();
        base.OnFormClosing(e);
    }
}
