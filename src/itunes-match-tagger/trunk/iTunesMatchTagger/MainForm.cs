using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using iTunesLib;
using System.Collections.Specialized;
using System.Threading;

namespace iTunesMatchTagger
{
    public partial class MainForm : Form
    {
        private List<UpdateOption> _options = new List<UpdateOption>();
        private StringCollection _selectedStoreCountries = new StringCollection();
        private List<Track> _tracks = new List<Track>();
        public iTunesApp iTunesApp = new iTunesApp();
        
        public MainForm()
        {
            InitializeComponent();

            this.Text += " - v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + " by Martin Pietschmann";

            Logging.OnWriteLog += new Logging.WriteLogEventHandler(Logging_OnWriteLog);
            //_helper.OniTunesLookup += new iTunesHelper.iTunesLookupEventHandler(iTunesHelper_OniTunesLookup);

            lbCountries.DataSource = iTunesHelper.StoreCountries;
            // select first 7 countries
            for (int i = 0; i < 8; i++)
            {
                lbCountries.SetSelected(i, true);
            }

            InitUpdateOptions();
            GenerateColumns();
        }

        private void InitUpdateOptions()
        {
            _options.Add(new UpdateOption("Track Name", true, "trackName", "Name"));
            _options.Add(new UpdateOption("Track Artist", true, "artistName", "Artist"));
            _options.Add(new UpdateOption("Album Artist", true, "AlbumArtist", "AlbumArtist"));
            _options.Add(new UpdateOption("Album Name", true, "collectionName", "Album"));
            _options.Add(new UpdateOption("Year", true, "year", "Year"));
            _options.Add(new UpdateOption("Genre", true, "primaryGenreName", "Genre"));
            _options.Add(new UpdateOption("Track Number", false, "trackNumber", "TrackNumber"));
            _options.Add(new UpdateOption("Track Count", false, "trackCount", "TrackCount"));
            _options.Add(new UpdateOption("Disc Number", false, "discNumber", "DiscNumber"));
            _options.Add(new UpdateOption("Disc Count", false, "discCount", "DiscCount"));
            _options.Add(new UpdateOption("Filename", false, "Filename", "Location", false, "[unknown]"));

            gridUpdateOptions.AutoGenerateColumns = false;
            gridUpdateOptions.DataSource = _options.Where(x => x.ShowOption == true).ToList();//_options;
            PopulateUpdateOptionsDropDown();
        }

        private void GenerateColumns()
        {
            gridTracks.AutoGenerateColumns = false;
            foreach (var option in _options)
            {
                var col = new DataGridViewTextBoxColumn();
                col.HeaderText = option.HeaderText;
                col.Name = option.ITunesComMember;
                col.DataPropertyName = option.LookupMember;
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
                col.DefaultCellStyle.NullValue = option.NullValue;

                gridTracks.Columns.Add(col);
            }
        }

        /// <summary>
        /// populate overwrite dropdown
        /// </summary>
        private void PopulateUpdateOptionsDropDown()
        {
            foreach (DataGridViewRow row in gridUpdateOptions.Rows)
            {
                try
                {
                    var option = ((UpdateOption)row.DataBoundItem);
                    var cb_cell = (DataGridViewComboBoxCell)row.Cells["OverwriteValueList"];
                    cb_cell.Items.Clear();
                    cb_cell.Items.Add("");
                    foreach (var item in _tracks)
	                {
                        var value = item.GetType().GetProperty(option.LookupMember).GetValue(item, null);
		                if (value != null && !cb_cell.Items.Contains(value)) cb_cell.Items.Add(value);
	                }

                    foreach (var item in _tracks.Select(x => x.iTunesTrack))
                    {
                        var value = item.GetType().InvokeMember(option.ITunesComMember, System.Reflection.BindingFlags.GetProperty, null, item, null);
                        if (value != null && !cb_cell.Items.Contains(value)) cb_cell.Items.Add(value);
                    }
                }
                catch (Exception ex)
                {
                    Logging.WriteLog(this, ex.Message, Logging.Severity.Debug);
                }
            }
        }

        /// <summary>
        /// select all countries
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < lbCountries.Items.Count; i++)
            {
                lbCountries.SetSelected(i, true);
            }
        }

        private void btnGetTracks_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            ClearLog();
            gridTracks.DataSource = null;
            gridTracks.Refresh();

            var tracks = iTunesApp.SelectedTracks;
            if (tracks == null)
            {
                Logging.WriteLog(this, "No tracks selected.", Logging.Severity.Error);
                MessageBox.Show("No tracks selected.");
                return;
            }

            Logging.WriteLog(this, tracks.Count + " tracks selected.", Logging.Severity.Information);

            _tracks.Clear();
            foreach (IITTrack item in tracks)
            {
                _tracks.Add(new Track(item, _options));
            }

            PopulateUpdateOptionsDropDown();
            gridTracks.DataSource = _tracks;
            gridTracks.Refresh();

            foreach (DataGridViewRow row in gridTracks.Rows)
            {
                Track track = (Track)row.DataBoundItem;
                if (!track.IsDownloaded())
                {
                    row.ErrorText = "Track is not downloaded!";
                }
                else if (!track.IsMatched())
                {
                    row.ErrorText = "Track is not matched!";
                }
                else if (!track.IsValid())
                {
                    row.ErrorText = "TrackID not found!";
                }
                if (row.ErrorText != "")
                {
                    Logging.WriteLog(this, row.ErrorText + " " + track.trackName, Logging.Severity.Debug);
                    row.ErrorText += " Unable to lookup track on iTunes.";
                }
            }

            Cursor.Current = Cursors.Default;
        }

        /// <summary>
        /// show info form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnInfo_Click(object sender, EventArgs e)
        {
            var info = new Info();
            info.ShowDialog();
        }

        /// <summary>
        /// lookup all tracks in itunes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnLookupTracks_Click(object sender, EventArgs e)
        {
            _selectedStoreCountries = new StringCollection();
            foreach (var item in lbCountries.SelectedItems)
            {
                _selectedStoreCountries.Add(item.ToString());
            }

            // check store countries
            if (_selectedStoreCountries.Count == 0)
            {
                Logging.WriteLog(this, "No store countries selected.", Logging.Severity.Error);
                MessageBox.Show("No store countries selected.");
                return;
            }

            // check update flag
            if (!_options.Any(x => x.Update == true))
            {
                Logging.WriteLog(this, "No update options selected!", Logging.Severity.Error);
                MessageBox.Show("No update options selected!");
                return;
            }

            progressBar1.Value = 0;
            progressBar1.Maximum = _tracks.Count;
            Cursor.Current = Cursors.WaitCursor;
            Logging.WriteLog(this, "Lookup started", Logging.Severity.Information);

            // lookup track on iTunes
            foreach (var track in _tracks)
            {
                Thread t = new Thread(() => LookupTrackAsync(track, _selectedStoreCountries));
                t.Start();
            }
        }

        private void LookupTrackAsync(Track track, StringCollection countries)
        {
            if (!track.IsValid())
            {
                Logging.WriteLog(this, "Unable to lookup " + track.trackName + "!", Logging.Severity.Warning);
                Invoke(new MethodInvoker(LookupNextStep));
            }
            else
            {
                var helper = new iTunesHelper();
                helper.OniTunesLookup += new iTunesHelper.iTunesLookupEventHandler(iTunesHelper_OniTunesLookup);
                helper.LookupTrack(track, _selectedStoreCountries);
            }
        }

        /// <summary>
        /// write id3tags to file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnUpdateTracks_Click(object sender, EventArgs e)
        {
            // check if any update flag is set to true
            if (!_options.Any(x => x.Update == true))
            {
                Logging.WriteLog(this, "No update options selected!", Logging.Severity.Error);
                MessageBox.Show("No update options selected!");
                return;
            }

            Cursor.Current = Cursors.WaitCursor;
            progressBar1.Value = 0;
            progressBar1.Maximum = _tracks.Count;
            progressBar1.Refresh();
            Logging.WriteLog(this, "Update started", Logging.Severity.Information);

            foreach (Track track in _tracks)
            {
                track.UpdateTrack(_options);
                progressBar1.PerformStep();

                // Zeile aktualisieren
                gridTracks.Refresh();
            }

            Cursor.Current = Cursors.Default;
            Logging.WriteLog(this, "Update complete", Logging.Severity.Information);
            MessageBox.Show("Update complete!");
        }

        private void iTunesHelper_OniTunesLookup(object sender, iTunesHelper.iTunesLookupEventArgs args)
        {
            var track = args.Track;
            var result = args.LookupResult;
            string trackname = track.Filename != null ? track.Filename : track.trackName;
            Logging.WriteLog(this, "iTunesLookup: Store='" + args.Country + "' Track='" + trackname + "'", Logging.Severity.Debug);

            if (result != null)
            {
                // Werte übertragen
                foreach (var option in _options.Where(x => x.Update == true))
                {
                    var prop = result.GetType().GetProperty(option.LookupMember);
                    if (prop != null)
                    {
                        var value = prop.GetValue(result, null);
                        track.GetType().GetProperty(option.LookupMember).SetValue(track, value, null);
                    }
                }
                track.LookupSuccess = true;
                Invoke(new MethodInvoker(refreshGrid));

                Logging.WriteLog(this, track.Filename + " found", Logging.Severity.Debug);
            }
            else
            {
                Logging.WriteLog(this, track.Filename + " not found", Logging.Severity.Information);
            }
            Invoke(new MethodInvoker(LookupNextStep));
        }
        
        private void refreshGrid()
        {
        }

        private int _lookupCounter = 0;
        private void LookupNextStep()
        {
            _lookupCounter++;
            progressBar1.PerformStep();

            // lookup complete
            if (_lookupCounter == gridTracks.Rows.Count)
            {
                _lookupCounter = 0;
                PopulateUpdateOptionsDropDown();
                Logging.WriteLog(this, _tracks.Count(x => x.LookupSuccess == true) + " of " + gridTracks.Rows.Count + " tracks found", Logging.Severity.Information);

                gridTracks.Refresh();
                gridTracks.AutoResizeColumns();

                Cursor.Current = Cursors.Default;
            }
        }

        delegate void Logging_OnWriteLog_Callback(object sender, Logging.LoggingEventArgs args);
        private void Logging_OnWriteLog(object sender, Logging.LoggingEventArgs args)
        {
            if (tbLog.InvokeRequired)
            {
                Invoke(new Logging_OnWriteLog_Callback(Logging_OnWriteLog), new []{ sender, args });
            }
            else
            {
                string text = "";
                if (args.Severity == Logging.Severity.Debug && !cbShowDebug.Checked) return;
                if (args.Severity == Logging.Severity.Error) text = "Error: ";
                if (args.Severity == Logging.Severity.Warning) text = "Warning: ";
                text += args.Message + "\r\n";

                tbLog.Text += text;
                tbLog.SelectionStart = tbLog.Text.Length;
                tbLog.ScrollToCaret();
                tbLog.Refresh();
            }
        }

        public void ClearLog()
        {
            tbLog.Text = "";
        }

        #region gridUpdateOptions
        private void gridUpdateOptions_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            Logging.WriteLog(this, e.Exception.Message, Logging.Severity.Debug);
        }

        private ComboBox cb;

        void cb_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                // set selected value to value column
                gridUpdateOptions.CurrentRow.Cells["OverwriteValue"].Value = cb.Text;
                cb.SelectedIndexChanged -= new EventHandler(cb_SelectedIndexChanged);
                cb.Text = "";
                cb.SelectedIndexChanged += new EventHandler(cb_SelectedIndexChanged);
            }
            catch (Exception ex)
            {
                Logging.WriteLog(this, ex.Message, Logging.Severity.Error);
            }
        }

        private void gridUpdateOptions_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (gridUpdateOptions.CurrentCell == gridUpdateOptions.CurrentRow.Cells["OverwriteValueList"])
            {
                cb = e.Control as ComboBox;
                if (cb != null)
                {
                    cb.SelectedIndexChanged -= new EventHandler(cb_SelectedIndexChanged);
                    cb.SelectedIndexChanged += new EventHandler(cb_SelectedIndexChanged);
                }
            }
        }

        private void gridUpdateOptions_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (gridUpdateOptions[e.ColumnIndex, e.RowIndex] == gridUpdateOptions.CurrentRow.Cells["OverwriteValue"])
                {
                    // check overwrite checkbox
                    gridUpdateOptions.CurrentRow.Cells["Overwrite"].Value = true;
                }
            }
            catch (Exception ex)
            {
                Logging.WriteLog(this, ex.Message, Logging.Severity.Error);
            }
        }
        #endregion
    }
}