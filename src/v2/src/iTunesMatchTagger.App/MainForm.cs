using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using iTunesMatchTagger.Core.Fields;
using iTunesMatchTagger.Core.ITunes;
using iTunesMatchTagger.Core.Lookup;
using iTunesMatchTagger.Core.Settings;
using iTunesMatchTagger.Core.Sources;
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

    private sealed record LookupOutcome(
        TrackRow Row,
        IReadOnlyList<TagCandidate>? Candidates,
        string FoundIn,
        bool FoundViaSearch,
        long? AppleTrackId,
        IReadOnlyDictionary<int, byte[]> ArtworkPreviews);

    private readonly ITunesComClient _itunes = new();
    private readonly ITunesSearchClient _search = new();
    private readonly LyricsClient _lyrics = new();
    private readonly HttpClient _artworkHttp = new();
    private readonly List<TrackRow> _rows = [];
    private readonly BindingList<OptionRow> _options = [];
    private readonly IProgress<LogEntry> _logSink;
    private readonly IProgress<LookupOutcome> _lookupSink;
    private CancellationTokenSource? _cancellation;
    private bool _busy;

    private AppSettings _settings = new();
    private CheckedListBox _sourcesList = new();
    private CheckBox _alwaysQueryAll = new();
    private CheckBox _includeLyrics = new();
    private TextBox _discogsToken = new();
    private ComboBox _candidatePicker = new();
    private DataGridView _optionsGrid = new();
    private ListBox _trackList = new();
    private SplitContainer _splitter = new();
    private DataGridView _detailGrid = new();
    private readonly Dictionary<string, DataGridViewColumn> _sourceColumns = new(StringComparer.Ordinal);
    private Panel _artworkStrip = new();
    private readonly Dictionary<string, PictureBox> _sourceArtworkPics = new(StringComparer.Ordinal);
    private PictureBox _currentArtworkStripPic = new();
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
        // 1010 leaves the comparison grid enough rows under the artwork strip
        // on a 1080p screen; clamp to the working area so the bottom bar and
        // log stay reachable
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1380, 940);
        ClientSize = new Size(1380, Math.Min(1010, workArea.Height - 60));
        MinimumSize = new Size(1160, 720);

        BuildLayout();

        foreach (var field in TrackFields.Visible)
        {
            _options.Add(new OptionRow(field));
        }

        LoadSettings();
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

        var countries = _settings.SelectedCountries;
        var fallbackSources = BuildSourceChain(out var sourceErrors);
        if (countries.Count == 0 && fallbackSources.Count == 0)
        {
            var message = sourceErrors.Count > 0
                ? $"No store countries selected and no tag sources ready:\n{string.Join("\n", sourceErrors)}"
                : "No store countries selected and no tag sources enabled.";
            Log(message, LogSeverity.Error);
            MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (countries.Count == 0)
        {
            Log("No store countries selected - Apple lookup will be skipped.", LogSeverity.Warning);
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
        Log($"Lookup started: {_rows.Count} track(s), {countries.Count} countr(ies), {fallbackSources.Count} fallback source(s).");
        var skippedSources = new HashSet<string>(StringComparer.Ordinal);
        var alwaysQueryAll = _alwaysQueryAll.Checked;

        try
        {
            await Parallel.ForEachAsync(
                _rows,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = _cancellation.Token },
                async (row, cancellationToken) =>
                {
                    var outcome = await LookupRowAsync(row, countries, fallbackSources, alwaysQueryAll, skippedSources, cancellationToken).ConfigureAwait(false);
                    _lookupSink.Report(outcome);
                }).ConfigureAwait(true);

            Log($"{_rows.Count(static r => r.LookupSuccess)} of {_rows.Count} track(s) found.");
        }
        catch (OperationCanceledException)
        {
            Log("Lookup cancelled.", LogSeverity.Warning);
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

    /// <summary>
    /// Builds the live fallback chain from the sources panel (Apple is
    /// implicit and always first). Reports configuration problems for
    /// enabled-but-unready sources (missing Discogs token).
    /// </summary>
    private List<ITagSource> BuildSourceChain(out List<string> errors)
    {
        errors = [];
        var chain = new List<ITagSource>();
        foreach (var id in CheckedSourceIds())
        {
            var source = TagSourceFactory.Create(TagSourceCatalog.ById(id)!, _discogsToken.Text.Trim());
            if (source is null)
            {
                continue;
            }

            if (source.RequiresCredentials && string.IsNullOrWhiteSpace(_discogsToken.Text))
            {
                errors.Add($"{source.Description}: needs an access token.");
                source.Dispose();
                continue;
            }

            chain.Add(source);
        }

        return chain;
    }

    private List<string> CheckedSourceIds()
    {
        var ids = new List<string>();
        foreach (var item in _sourcesList.CheckedItems)
        {
            if (item is TagSourceInfo info)
            {
                ids.Add(info.Id);
            }
        }

        return ids;
    }

    private async Task<LookupOutcome> LookupRowAsync(
        TrackRow row,
        IReadOnlyList<string> countries,
        IReadOnlyList<ITagSource> fallbackSources,
        bool alwaysQueryAll,
        ISet<string> skippedSources,
        CancellationToken cancellationToken)
    {
        // Stage 1 - Apple first: the embedded catalog ID across storefronts.
        ITunesLookupResult? appleResult = null;
        var foundIn = string.Empty;
        var viaSearch = false;

        if (row.Track.TrackId > 0 && countries.Count > 0)
        {
            foreach (var country in countries)
            {
                var found = await _search.LookupTrackAsync(row.Track.TrackId, country, cancellationToken).ConfigureAwait(false);
                if (found is not null)
                {
                    LogDebug($"Track ID {row.Track.TrackId} found in {country}: {row.File}");
                    appleResult = found;
                    foundIn = country;
                    break;
                }
            }

            if (appleResult is null)
            {
                // The embedded ID is dead in every configured storefront (Apple
                // delists albums; the file keeps the old ID). Sweep the
                // remaining storefronts before giving up - regional catalogs
                // differ, so a delisted album may still live somewhere else.
                Log($"Track ID {row.Track.TrackId} not found in the configured countries, sweeping the other storefronts: {row.File}", LogSeverity.Information);
                var tried = countries.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var country in StoreCountries.All.Where(c => !tried.Contains(c)))
                {
                    var found = await _search.LookupTrackAsync(row.Track.TrackId, country, cancellationToken).ConfigureAwait(false);
                    if (found is not null)
                    {
                        LogDebug($"Track ID {row.Track.TrackId} found in {country} (sweep): {row.File}");
                        appleResult = found;
                        foundIn = country;
                        break;
                    }
                }
            }

            if (appleResult is null)
            {
                // The ID is dead everywhere. Fall back to searching by the
                // current tags - re-released albums come back under a new ID
                // that an ID lookup would never find.
                Log($"Track ID {row.Track.TrackId} not found in any storefront, trying search by current tags: {row.File}", LogSeverity.Information);
            }
        }

        // Stage 2 - Apple term search across storefronts.
        if (appleResult is null && countries.Count > 0)
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
                    var first = results.FirstOrDefault(static r =>
                        (r.Kind is null or "song") && !string.IsNullOrWhiteSpace(r.TrackName));
                    if (first is not null)
                    {
                        LogDebug($"Term '{term}' found in {country}: {row.File} (catalog Track ID {first.TrackId})");
                        appleResult = first;
                        foundIn = country;
                        viaSearch = true;
                        break;
                    }
                }
            }
        }

        // Stage 3 - the other enabled tag sources, in fallback order.
        // Every enabled source is queried (not just the first that finds
        // something) so the user can compare candidates across sources.
        // With alwaysQueryAll the sources run even after an Apple hit.
        var candidates = new List<TagCandidate>();
        var autoSelectIndex = -1;

        if (appleResult is not null)
        {
            candidates.Add(ITunesSource.FromLookupResult(appleResult));
            autoSelectIndex = 0;
        }

        if ((appleResult is null || alwaysQueryAll) && fallbackSources.Count > 0)
        {
            var term = new TagQuery(
                Artist: row.GetCurrent("artistName"),
                Title: row.GetCurrent("trackName"),
                Album: row.GetCurrent("collectionName"));
            if (string.IsNullOrWhiteSpace(term.ToString()))
            {
                Log($"No tags to search on other sources: {row.File}", LogSeverity.Warning);
            }
            else
            {
                foreach (var source in fallbackSources)
                {
                    if (skippedSources.Contains(source.Id))
                    {
                        continue;
                    }

                    try
                    {
                        var sourceCandidates = await source.SearchAsync(term, cancellationToken).ConfigureAwait(false);
                        if (sourceCandidates.Count > 0)
                        {
                            Log($"{source.Id}: {sourceCandidates.Count} candidate(s): {row.File}");
                            if (autoSelectIndex < 0)
                            {
                                autoSelectIndex = candidates.Count;
                            }

                            candidates.AddRange(sourceCandidates);
                        }
                        else
                        {
                            LogDebug($"{source.Id}: no results: {row.File}");
                        }
                    }
                    catch (TagSourceAuthException ex)
                    {
                        // one message per run - it is a configuration problem
                        skippedSources.Add(source.Id);
                        Log($"{source.Id} disabled for this run: {ex.Message}", LogSeverity.Error);
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TagSourceException or TaskCanceledException)
                    {
                        Log($"{source.Id} lookup failed: {ex.Message}", LogSeverity.Warning);
                    }
                }
            }
        }

        // 300px preview for every source's displayed candidate (its first by
        // default), so the artwork strip can show all sources at once
        var previews = new Dictionary<int, byte[]>();
        foreach (var sourceId in _sourceColumns.Keys)
        {
            var candidateIndex = candidates.FindIndex(c => c.SourceId == sourceId);
            if (candidateIndex < 0 || candidates[candidateIndex].ArtworkUrl is not { } url)
            {
                continue;
            }

            try
            {
                previews[candidateIndex] = await DownloadArtworkAsync(url, 300, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                LogDebug($"Artwork preview download failed ({sourceId}): {ex.Message}");
            }
        }

        return new LookupOutcome(
            row,
            candidates,
            foundIn,
            viaSearch,
            appleResult?.TrackId,
            previews);
    }

    /// <summary>
    /// Downloads artwork bytes; Apple URLs get their size segment rewritten
    /// (100x100bb -> 300x300bb), other sources use the URL as-is.
    /// </summary>
    private async Task<byte[]> DownloadArtworkAsync(string artworkUrl, int size, CancellationToken cancellationToken)
    {
        var url = ITunesSearchClient.SizedArtworkUrl(artworkUrl, size);
        return await _artworkHttp.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs on the UI thread (posted through <see cref="Progress{T}"/>).</summary>
    private void ApplyLookupOutcome(LookupOutcome outcome)
    {
        var row = outcome.Row;
        _progress.PerformStep();

        var candidates = outcome.Candidates ?? [];
        if (candidates.Count > 0)
        {
            row.LookupSuccess = true;
            row.SetCandidates(candidates, 0);

            foreach (var (candidateIndex, bytes) in outcome.ArtworkPreviews)
            {
                row.SetCandidateArtwork(candidateIndex, ToImage(bytes));
            }

            row.SetStatus(BuildLookupStatus(row, outcome), StatusKind.Success);
        }
        else
        {
            row.LookupSuccess = false;
            row.ClearProposed();
            row.SetCandidates([], -1);
            row.SetStatus("Not found on Apple or the enabled tag sources", StatusKind.Error);
            Log($"Not found: {row.File} (Track ID {row.Track.TrackId})", LogSeverity.Information);
        }

        _trackList.Invalidate();
        if (ReferenceEquals(SelectedRow(), row))
        {
            FillDetail(row);
        }
    }

    private string BuildLookupStatus(TrackRow row, LookupOutcome outcome)
    {
        var active = row.ActiveCandidate!;
        return active.SourceId == TagSources.ITunes
            ? outcome.FoundViaSearch
                ? $"Found via search in {outcome.FoundIn} (catalog ID {outcome.AppleTrackId})"
                : $"Found in {outcome.FoundIn}"
            : $"Found on {active.SourceId} - {row.Candidates.Count} candidate(s), pick one in the detail panel";
    }

    /// <summary>
    /// Looks up lyrics on LRCLib using the proposed (or current) tags and
    /// writes them with synced LRC preferred over plain text (Apple's
    /// syllable-level karaoke text is not publicly available).
    /// Returns true when lyrics were written.
    /// </summary>
    private async Task<bool> WriteLyricsIfNeededAsync(TrackRow row)
    {
        // an empty candidate value ("") must not shadow the current tag
        var title = FirstNonEmpty(row.GetProposed("trackName"), row.GetCurrent("trackName"));
        var artist = FirstNonEmpty(row.GetProposed("artistName"), row.GetCurrent("artistName"));
        var album = FirstNonEmpty(row.GetProposed("collectionName"), row.GetCurrent("collectionName"));
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
        {
            Log($"No tags to look up lyrics for: {row.File}", LogSeverity.Warning);
            return false;
        }

        var lyrics = await _lyrics.LookupAsync(artist, title, album, row.Track.DurationMs, CancellationToken.None).ConfigureAwait(true);
        if (lyrics is null)
        {
            Log($"No lyrics found on LRCLib: {row.File}", LogSeverity.Warning);
            return false;
        }

        if (lyrics.IsEmpty)
        {
            Log($"Instrumental track, nothing to write: {row.File}");
            return false;
        }

        var text = lyrics.Best!;
        await Task.Run(() => row.Track.WriteLyrics(text)).ConfigureAwait(true);
        var lineCount = text.Count(static l => l == '\n') + 1;
        Log($"Lyrics ({(lyrics.Synced is null ? "plain" : "synced LRC")}, {lineCount} lines, LRCLib #{lyrics.TrackId}) -> {row.File}");
        return true;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v));

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
        var lyricsWrittenCount = 0;
        var lyricsMissedCount = 0;

        try
        {
            foreach (var row in _rows)
            {
                if (row.SkipUpdate)
                {
                    row.SetStatus("Excluded by the user's checkbox", StatusKind.Neutral);
                    LogDebug($"Skipped (excluded): {row.File}");
                    _progress.PerformStep();
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
                            var bytes = await DownloadArtworkAsync(artworkValue, 600, CancellationToken.None).ConfigureAwait(true);
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

                var lyricsWritten = false;
                if (_includeLyrics.Checked)
                {
                    try
                    {
                        lyricsWritten = await WriteLyricsIfNeededAsync(row).ConfigureAwait(true);
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
                        Log($"Unable to write lyrics for '{row.File}': {ex.Message}", LogSeverity.Error);
                    }
                }

                var written = fieldsWritten + (artworkWritten ? 1 : 0) + (lyricsWritten ? 1 : 0);
                row.SetStatus(
                    written > 0 ? $"Updated ({written} change(s))" : "Nothing to update",
                    written > 0 ? StatusKind.Success : StatusKind.Warning);

                // re-read the current values so the comparison shows the result
                foreach (var field in TrackFields.All)
                {
                    row.SetCurrent(field.LookupMember, row.Track.ReadField(field)?.ToString());
                }

                _progress.PerformStep();
            }

            if (_includeLyrics.Checked)
            {
                Log($"Lyrics summary: {lyricsWrittenCount} written, {lyricsMissedCount} without match/instrumental/without tags.");
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
        RefreshCandidatePicker(row);
        RefreshCompareGrid(row);

        if (row is null)
        {
            _currentArtworkStripPic.Image = null;
            foreach (var pic in _sourceArtworkPics.Values)
            {
                pic.Image = null;
            }

            _statusLabel.Text = "Select a track to review its tags.";
            _statusLabel.ForeColor = SystemColors.ControlText;
            AlignArtworkStrip();
            return;
        }

        _currentArtworkStripPic.Image = row.CurrentArtworkImage;
        RefreshArtworkStrip(row);

        var text = row.StatusMessage.Length == 0
            ? "No lookup yet - click \"2. Lookup tracks\"."
            : row.StatusMessage;
        if (row.SkipUpdate)
        {
            text += "   [excluded from update]";
        }

        _statusLabel.Text = text;
        _statusLabel.ForeColor = row.StatusSeverity switch
        {
            StatusKind.Success => Color.FromArgb(0, 128, 0),
            StatusKind.Warning => Color.FromArgb(176, 96, 0),
            StatusKind.Error => Color.FromArgb(192, 0, 0),
            _ => SystemColors.ControlText,
        };
        AlignArtworkStrip();
    }

    /// <summary>Sets each strip picture from the candidate it shows.</summary>
    private void RefreshArtworkStrip(TrackRow row)
    {
        foreach (var (sourceId, pic) in _sourceArtworkPics)
        {
            var candidateIndex = row.CandidateIndexForSource(sourceId);
            pic.Image = candidateIndex < 0 ? null : row.GetCandidateArtwork(candidateIndex);
        }
    }

    /// <summary>Short strip label for a source.</summary>
    private static string StripLabel(TagSourceInfo source) => source.Id == TagSources.ITunes ? "Apple" : source.DisplayName;

    private void RefreshCandidatePicker(TrackRow? row)
    {
        _candidatePicker.SelectedIndexChanged -= CandidatePicker_SelectedIndexChanged;
        _candidatePicker.Items.Clear();
        _candidatePicker.Enabled = row is { Candidates.Count: > 0 };

        if (row is { Candidates.Count: > 0 })
        {
            for (var i = 0; i < row.Candidates.Count; i++)
            {
                var candidate = row.Candidates[i];
                var summary = string.Join(" - ",
                    new[] { candidate.Title, candidate.Artist, candidate.Album }
                        .Where(static s => !string.IsNullOrEmpty(s)));
                _candidatePicker.Items.Add($"[{candidate.SourceId}] {summary}");
            }

            _candidatePicker.SelectedIndex = Math.Max(0, row.ActiveCandidateIndex);
        }

        _candidatePicker.SelectedIndexChanged += CandidatePicker_SelectedIndexChanged;
    }

    private void CandidatePicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var row = SelectedRow();
        if (row is null || row.Candidates.Count == 0)
        {
            return;
        }

        var index = _candidatePicker.SelectedIndex;
        if (index < 0 || index == row.ActiveCandidateIndex)
        {
            return;
        }

        row.SelectCandidate(index);
        _trackList.Invalidate();
        RefreshCompareGrid(row);
        HighlightActiveSource(row.ActiveCandidate?.SourceId);

        var candidate = row.ActiveCandidate;
        if (candidate is not null && row.GetCandidateArtwork(index) is null && candidate.ArtworkUrl is not null)
        {
            _ = LoadCandidateArtworkAsync(row, index, candidate.ArtworkUrl);
        }
    }

    /// <summary>Downloads a candidate's artwork preview and updates its picture when done.</summary>
    private async Task LoadCandidateArtworkAsync(TrackRow row, int candidateIndex, string artworkUrl)
    {
        try
        {
            var bytes = await DownloadArtworkAsync(artworkUrl, 300, CancellationToken.None).ConfigureAwait(true);
            var image = ToImage(bytes);
            BeginInvoke(new Action(() =>
            {
                row.SetCandidateArtwork(candidateIndex, image);
                RefreshArtworkStrip(row);
                if (row.ActiveCandidateIndex == candidateIndex)
                {
                    _trackList.Invalidate();
                }
            }));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            LogDebug($"Candidate artwork download failed: {ex.Message}");
        }
    }

    private void RefreshCompareGrid(TrackRow? row)
    {
        _detailGrid.Rows.Clear();

        if (row is null)
        {
            return;
        }

        foreach (var field in TrackFields.All)
        {
            if (field.LookupMember == "Filename" || field == TrackFields.Artwork)
            {
                continue; // the list shows the file; artwork is shown in the strip
            }

            var current = row.GetCurrent(field.LookupMember);

            // per-source cells: each shows the source's picked candidate
            // value, empty when that source produced nothing (still visible)
            var values = new object[3 + _sourceColumns.Count];
            values[0] = row.IsFieldEnabled(field.LookupMember);
            values[1] = field.DisplayName;
            values[2] = current ?? string.Empty;

            var columnIndex = 3;
            foreach (var sourceId in _sourceColumns.Keys)
            {
                var candidate = row.CandidateForSource(sourceId);
                var value = candidate is null ? string.Empty : field.GetFromCandidate?.Invoke(candidate)?.ToString() ?? string.Empty;
                values[columnIndex] = value;
                columnIndex++;
            }

            var index = _detailGrid.Rows.Add(values!);

            columnIndex = 3;
            foreach (var sourceId in _sourceColumns.Keys)
            {
                var cell = _detailGrid.Rows[index].Cells[columnIndex];
                var candidate = row.CandidateForSource(sourceId);
                if (candidate is null)
                {
                    cell.Style.ForeColor = SystemColors.ControlDark; // source had no results
                }
                else
                {
                    var proposed = (string?)cell.Value;
                    if (!string.IsNullOrEmpty(proposed))
                    {
                        var changed = !string.Equals(current, proposed, StringComparison.Ordinal);
                        cell.Style.BackColor = changed
                            ? Color.FromArgb(255, 246, 220) // will change - amber
                            : Color.FromArgb(232, 245, 233); // identical - light green
                    }
                }

                columnIndex++;
            }

            if (!row.IsFieldEnabled(field.LookupMember))
            {
                _detailGrid.Rows[index].DefaultCellStyle.ForeColor = SystemColors.GrayText;
            }
        }

        HighlightActiveSource(row.ActiveCandidate?.SourceId);
    }

    /// <summary>Marks the active candidate's source column as the write target.</summary>
    private void HighlightActiveSource(string? activeSourceId)
    {
        foreach (var (sourceId, column) in _sourceColumns)
        {
            var active = sourceId == activeSourceId;
            column.HeaderCell.Style.BackColor = active ? Color.FromArgb(255, 224, 178) : Color.Empty;
            column.HeaderCell.Style.ForeColor = active ? Color.FromArgb(120, 70, 0) : Color.Empty;
            column.HeaderCell.Style.Font = new Font(_detailGrid.Font, active ? FontStyle.Bold : FontStyle.Regular);
        }
    }

    private bool WillChangeSomething(TrackRow row)
    {
        if (row.SkipUpdate)
        {
            return false; // excluded tracks never show the change dot
        }

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
        if (row.SkipUpdate)
        {
            // excluded tracks read as disabled
            foreColor = selected ? Color.FromArgb(214, 214, 214) : SystemColors.GrayText;
        }

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
        var textWidth = Math.Max(10, e.Bounds.Width - textLeft - 62); // room for the skip checkbox + change dot
        using (var nameFont = new Font("Segoe UI", 9.5f, row.SkipUpdate ? FontStyle.Strikeout : FontStyle.Bold))
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
            e.Graphics.FillEllipse(dotBrush, SkipRect(e.Bounds).Left - 14, e.Bounds.Top + 9, 9, 9);
        }

        DrawSkipCheckbox(e, row, skipRect: SkipRect(e.Bounds));

        if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
        {
            e.DrawFocusRectangle();
        }
    }

    /// <summary>Location of the per-row skip-update checkbox (top right).</summary>
    private static Rectangle SkipRect(Rectangle itemBounds) => new(itemBounds.Right - 24, itemBounds.Top + 5, 16, 16);

    private static void DrawSkipCheckbox(DrawItemEventArgs e, TrackRow row, Rectangle skipRect)
    {
        e.Graphics.FillRectangle(Brushes.White, skipRect);
        using var borderPen = new Pen(row.SkipUpdate ? Color.FromArgb(192, 0, 0) : Color.FromArgb(200, 200, 200));
        e.Graphics.DrawRectangle(borderPen, skipRect);

        if (row.SkipUpdate)
        {
            using var crossPen = new Pen(Color.FromArgb(192, 0, 0), 2f);
            e.Graphics.DrawLine(crossPen, skipRect.Left + 3, skipRect.Top + 3, skipRect.Right - 4, skipRect.Bottom - 4);
            e.Graphics.DrawLine(crossPen, skipRect.Right - 4, skipRect.Top + 3, skipRect.Left + 3, skipRect.Bottom - 4);
        }
    }

    private void TrackList_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var index = _trackList.IndexFromPoint(e.Location);
        if (index < 0 || index >= _rows.Count)
        {
            return;
        }

        if (!SkipRect(_trackList.GetItemRectangle(index)).Contains(e.Location))
        {
            return;
        }

        var row = _rows[index];
        row.SkipUpdate = !row.SkipUpdate;
        _trackList.Invalidate();
        Log(row.SkipUpdate
            ? $"Excluded from update: {row.File}"
            : $"Included back: {row.File}");

        if (ReferenceEquals(SelectedRow(), row))
        {
            FillDetail(row);
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
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        var sourcesGroup = new GroupBox
        {
            Text = "Tag sources (fallback order)",
            Dock = DockStyle.Fill,
        };
        _sourcesList = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false,
            DisplayMember = nameof(TagSourceInfo.DisplayName),
        };
        _sourcesList.ItemCheck += SourcesList_ItemCheck;
        foreach (var source in TagSourceCatalog.All.Where(static s => s.Id != TagSources.ITunes))
        {
            _sourcesList.Items.Add(source, false);
        }

        var sourcesBottom = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            ColumnCount = 2,
            RowCount = 1,
            Height = 30,
            AutoSize = true,
        };
        sourcesBottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sourcesBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sourcesBottom.Controls.Add(new Label { Text = "Discogs token:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _discogsToken = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        var tokenTip = new ToolTip();
        tokenTip.SetToolTip(_discogsToken, "Personal access token from discogs.com/settings/developers");
        _discogsToken.Validated += DiscogsToken_Validated;
        sourcesBottom.Controls.Add(_discogsToken, 1, 0);

        _alwaysQueryAll = new CheckBox
        {
            Text = "Always query all enabled sources, even when Apple finds the track",
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 26,
        };
        _alwaysQueryAll.CheckedChanged += SettingsCheckedChanged;
        _includeLyrics = new CheckBox
        {
            Text = "Include lyrics from LRCLib (synced LRC when available, else plain text)",
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 26,
        };
        _includeLyrics.CheckedChanged += SettingsCheckedChanged;

        var sourcesHint = new Label
        {
            Text = "Apple (iTunes Search) is always tried first.",
            Dock = DockStyle.Top,
            Height = 20,
            ForeColor = SystemColors.GrayText,
        };

        sourcesGroup.Controls.Add(_sourcesList);
        sourcesGroup.Controls.Add(_alwaysQueryAll);
        sourcesGroup.Controls.Add(_includeLyrics);
        sourcesGroup.Controls.Add(sourcesBottom);
        sourcesGroup.Controls.Add(sourcesHint);

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

        panel.Controls.Add(sourcesGroup, 0, 0);
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
        _trackList.MouseDown += TrackList_MouseDown;
        _trackList.SelectedIndexChanged += TrackList_SelectedIndexChanged;
        var listTooltip = new ToolTip();
        listTooltip.SetToolTip(_trackList, "The box at the top-right of a track excludes it from '3. Update tracks' (click to toggle).");
        _splitter.Panel1.Controls.Add(_trackList);

        // Row 0: status ("Found...") with the Result picker docked at its
        // right; Row 1: the comparison grid stretched over the full width;
        // Row 2: artworks aligned under the grid's columns.
        var detail = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        detail.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));

        var statusBar = new Panel { Dock = DockStyle.Fill };
        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            AutoEllipsis = true,
            Padding = new Padding(4, 0, 0, 0),
        };

        var pickerHost = new Panel
        {
            Dock = DockStyle.Right,
            Width = 430,
            Padding = new Padding(0, 3, 0, 3),
        };
        var pickerFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        pickerFlow.Controls.Add(new Label
        {
            Text = "Result:",
            AutoSize = true,
            Margin = new Padding(3, 6, 4, 0),
            ForeColor = SystemColors.GrayText,
        });
        _candidatePicker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 370,
        };
        _candidatePicker.SelectedIndexChanged += CandidatePicker_SelectedIndexChanged;
        pickerFlow.Controls.Add(_candidatePicker);
        pickerHost.Controls.Add(pickerFlow);
        statusBar.Controls.Add(pickerHost);
        statusBar.Controls.Add(_statusLabel);
        detail.Controls.Add(statusBar, 0, 0);

        detail.Controls.Add(BuildCompareGrid(), 0, 1);
        detail.Controls.Add(BuildArtworkStrip(), 0, 2);

        _splitter.Panel2.Controls.Add(detail);

        return _splitter;
    }

    /// <summary>
    /// The row of artwork previews below the comparison grid: "Current" and
    /// one box per tag source. Each box is kept aligned with the matching
    /// grid column (pixel-geometry mirroring) so it is obvious which artwork
    /// came from which source.
    /// </summary>
    private Control BuildArtworkStrip()
    {
        _artworkStrip = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Control,
        };

        // Boxes are positioned manually (mirroring the grid columns), so
        // they must not be docked - Dock would fight SetBounds.
        var currentGroup = new GroupBox
        {
            Text = "Current",
            Dock = DockStyle.None,
            Bounds = new Rectangle(3, 3, 120, 120),
        };
        _currentArtworkStripPic = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Padding = new Padding(4),
        };
        currentGroup.Controls.Add(_currentArtworkStripPic);
        _artworkStrip.Controls.Add(currentGroup);

        foreach (var source in TagSourceCatalog.All)
        {
            var group = new GroupBox
            {
                Text = StripLabel(source),
                Dock = DockStyle.None,
                Bounds = new Rectangle(3, 3, 120, 120),
            };
            var pic = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Padding = new Padding(6),
            };
            group.Controls.Add(pic);
            _sourceArtworkPics[source.Id] = pic;
            _artworkStrip.Controls.Add(group);
        }

        _detailGrid.ColumnWidthChanged += (_, _) => AlignArtworkStrip();
        _detailGrid.ColumnDisplayIndexChanged += (_, _) => AlignArtworkStrip();
        _detailGrid.Scroll += (_, _) => AlignArtworkStrip();
        _detailGrid.Resize += (_, _) => AlignArtworkStrip();
        _splitter.SplitterMoved += (_, _) => AlignArtworkStrip();

        return _artworkStrip;
    }

    /// <summary>
    /// Mirrors each grid column's display rectangle onto its artwork box,
    /// taking the grid's horizontal scroll into account so boxes stay under
    /// the columns they belong to.
    /// </summary>
    private void AlignArtworkStrip()
    {
        if (_artworkStrip.Controls.Count == 0)
        {
            return;
        }

        var grid = _detailGrid;
        var currentColumn = grid.Columns["Current"];
        if (currentColumn is not null)
        {
            AlignOne(_currentArtworkStripPic.Parent!, currentColumn);
        }
        foreach (var (sourceId, column) in _sourceColumns)
        {
            if (_sourceArtworkPics.TryGetValue(sourceId, out var pic))
            {
                AlignOne(pic.Parent!, column);
            }
        }

        void AlignOne(Control box, DataGridViewColumn column)
        {
            var rect = grid.GetColumnDisplayRectangle(column.Index, false);
            var left = Math.Min(Math.Max(rect.Left + 2, 0), Math.Max(grid.ClientSize.Width - 20, 20));
            var right = Math.Min(rect.Right - 2, grid.ClientSize.Width);
            var width = Math.Max(right - left, 20);
            box.SetBounds(left, 3, width, _artworkStrip.Height - 34);
        }
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

        FillDetail(null); // also aligns the artwork strip boxes under the grid columns
    }

    private Control BuildCompareGrid()
    {
        var group = new GroupBox
        {
            Text = "Tag comparison (Current column, then one column per source)",
            Dock = DockStyle.Fill,
        };
        _detailGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            BackgroundColor = SystemColors.Window,
        };
        _detailGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Use",
            HeaderText = "Use",
            Width = 46,
        });
        _detailGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Field",
            HeaderText = "Field",
            ReadOnly = true,
            Width = 110,
        });
        _detailGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Current",
            HeaderText = "Current",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 42,
        });

        // one column per known source, in the catalog (fallback) order;
        // sources without results show empty cells on purpose
        _sourceColumns.Clear();
        foreach (var source in TagSourceCatalog.All)
        {
            var column = new DataGridViewTextBoxColumn
            {
                Name = $"Source_{source.Id}",
                HeaderText = source.Id == TagSources.ITunes ? "Apple" : source.DisplayName,
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 29,
            };
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _detailGrid.Columns.Add(column);
            _sourceColumns[source.Id] = column;
        }

        _detailGrid.CellValueChanged += DetailGrid_CellValueChanged;
        _detailGrid.CurrentCellDirtyStateChanged += static (s, e) =>
        {
            var grid = (DataGridView)s!;
            if (grid.IsCurrentCellDirty)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        group.Controls.Add(_detailGrid);
        return group;
    }

    private void DetailGrid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        var row = SelectedRow();
        if (row is null || e.RowIndex < 0 || _detailGrid.Columns[e.ColumnIndex].Name != "Use")
        {
            return;
        }

        var enabled = (bool)(_detailGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value ?? true);
        var fieldName = _detailGrid.Rows[e.RowIndex].Cells["Field"].Value?.ToString();
        var field = TrackFields.All.FirstOrDefault(f => f.DisplayName == fieldName);
        if (field is not null)
        {
            row.SetFieldEnabled(field.LookupMember, enabled);
            _trackList.Invalidate(); // the change dot may flip
            RefreshCompareGrid(row);
        }
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
        var settings = _settings = AppSettings.Load();

        foreach (var option in _options)
        {
            if (settings.Fields.TryGetValue(option.Field.LookupMember, out var saved))
            {
                option.Update = saved.Update;
                option.Overwrite = saved.Overwrite;
                option.OverwriteValue = saved.OverwriteValue;
            }
        }

        for (var i = 0; i < _sourcesList.Items.Count; i++)
        {
            if (_sourcesList.Items[i] is TagSourceInfo info)
            {
                var saved = settings.Sources.FirstOrDefault(s => s.Id == info.Id);
                _sourcesList.SetItemChecked(i, saved?.Enabled == true);
            }
        }

        _discogsToken.Text = settings.Sources.FirstOrDefault(static s => s.Id == TagSources.Discogs)?.Token ?? string.Empty;
        _alwaysQueryAll.Checked = settings.QueryAllSources;
        _includeLyrics.Checked = settings.IncludeLyrics;
    }

    /// <summary>Defers the save: CheckedItems is not updated until the check applies.</summary>
    private void SourcesList_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (IsHandleCreated)
        {
            BeginInvoke(new Action(SaveSettings));
        }
    }

    private void SettingsCheckedChanged(object? sender, EventArgs e) => SaveSettings();

    private void DiscogsToken_Validated(object? sender, EventArgs e) => SaveSettings();

    private void SaveSettings()
    {
        // SelectedCountries are hand-edited in settings.json; the UI never
        // rewrites them.
        var settings = _settings;
        settings.QueryAllSources = _alwaysQueryAll.Checked;
        settings.IncludeLyrics = _includeLyrics.Checked;
        List<TagSourceSettings> sources =
        [
            new() { Id = TagSources.ITunes, Enabled = true, Token = null },
            .. CheckedSourceIds().Select(static id => new TagSourceSettings { Id = id, Enabled = true }),
        ];

        if (_discogsToken.Text.Trim() is { Length: > 0 } token)
        {
            // One single Discogs entry - the token goes into the enabled
            // entry when the source is checked, otherwise a disabled entry
            // just preserves the token for the next session.
            var discogs = sources.FirstOrDefault(static s => s.Id == TagSources.Discogs);
            if (discogs is null)
            {
                sources.Add(new TagSourceSettings { Id = TagSources.Discogs, Enabled = false, Token = token });
            }
            else
            {
                discogs.Token = token;
            }
        }

        settings.Sources = sources;

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
