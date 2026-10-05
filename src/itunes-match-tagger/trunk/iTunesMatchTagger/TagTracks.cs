using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using iTunesLib;

namespace iTunesMatchTagger
{
    public partial class TagTracks : Form
    {
        public static iTunesHelper helper = new iTunesHelper();

        public TagTracks()
        {
            InitializeComponent();

            this.Text += " - v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + " by Martin Pietschmann";

            // helper events
            helper.OnWriteLog += new iTunesHelper.WriteLogEventHandler(iTunesHelper_OnWriteLog);
            helper.OniTunesLookup += new iTunesHelper.iTunesLookupEventHandler(iTunesHelper_OniTunesLookup);

            lbCountries.DataSource = iTunesHelper.AllCountries;
            // select first 7 countries
            for (int i = 0; i < 8; i++)
            {
                lbCountries.SetSelected(i, true);
            }
        }

        /// <summary>
        /// set update flags
        /// </summary>
        private void SetUpdateOptions()
        {
            helper.UpdateTrackName = cbUpdateTrackName.Checked;
            helper.UpdateCollectionName =cbUpdateCollectionName.Checked;
            helper.UpdateArtistName = cbUpdateArtistName.Checked;
            helper.UpdateTrackCount = cbUpdateTrackCount.Checked;
            helper.UpdateTrackNumber = cbUpdateTrackNumber.Checked;
            helper.UpdatePrimaryGenreName = cbUpdatePrimaryGenreName.Checked;
            helper.UpdateDiscCount = cbUpdateDiscCount.Checked;
            helper.UpdateDiscNumber = cbUpdateDiscNumber.Checked;
        }

        private void SetSelectedCountries()
        {
            helper.SelectedCountries = new List<string>();
            foreach (var item in lbCountries.SelectedItems)
            {
                helper.SelectedCountries.Add(item.ToString());
            }
        }

        private void btnUpdateTracks_Click(object sender, EventArgs e)
        {
            // init
            ClearLog();
            progressBar1.Value = 0;

            var tracks = helper.iTunes.SelectedTracks;
            if (tracks == null)
            {
                WriteLog("No tracks selected.");
                MessageBox.Show("No tracks selected.");
                return;
            }

            WriteLog(tracks.Count + " tracks selected.", false);

            SetUpdateOptions();
            SetSelectedCountries();

            if (!helper.IsUpdateOptionSet())
            {
                WriteLog("No update options selected!");
                MessageBox.Show("No update options selected!");
                return;
            }

            Cursor.Current = Cursors.WaitCursor;

            // search
            progressBar1.Maximum = tracks.Count * lbCountries.SelectedItems.Count;
            List<Track> tracks_found = helper.FindTags(tracks);

            // update
            progressBar1.Value = 0;
            progressBar1.Maximum = tracks_found.Count;
            progressBar1.Refresh();

            WriteLog("");
            WriteLog(tracks_found.Count + " tracks found");
            if (tracks_found.Count > 0)
            {
                WriteLog("");
                WriteLog("Updating:");

                // update
                foreach (Track track in tracks_found)
                {
                    WriteLog("> " + track.Id3Tag.artistName + " - " + track.Id3Tag.collectionName + " - " + track.Id3Tag.trackName);
                    helper.UpdateTrack(track.iTunesTrack, track.Id3Tag);
                    progressBar1.PerformStep();
                }
            }

            int tracks_not_found = tracks.Count - tracks_found.Count;
            if (tracks_not_found > 0)
            {
                WriteLog("");
                WriteLog(tracks_not_found + " tracks not found:");

                foreach (IITTrack track in tracks)
                {
                    try
                    {
                        if (tracks_found.FirstOrDefault(t => ((dynamic)t.iTunesTrack).Location == ((dynamic)track).Location) == null)
                        {
                            WriteLog("> " + ((dynamic)track).Location);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Error: " + ex.Message, true, true);
                    }
                }
            }

            Cursor.Current = Cursors.Default;
            MessageBox.Show("Update complete!");
        }

        protected void iTunesHelper_OniTunesLookup(IITTrack track, string country)
        {
            progressBar1.PerformStep();
        }

        public void iTunesHelper_OnWriteLog(string text, bool new_line = true, bool debug = false)
        {
            WriteLog(text, new_line, debug);
        }

        public void WriteLog(string text, bool new_line = true, bool debug = false)
        {
            if (debug && !cbShowDebug.Checked) return;
            tbLog.Text += (new_line ? "\r\n" : "") + text;

            tbLog.SelectionStart = tbLog.Text.Length;
            tbLog.ScrollToCaret();
            tbLog.Refresh();
        }

        public void ClearLog()
        {
            tbLog.Text = "";
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            // select all
            for (int i = 0; i < lbCountries.Items.Count; i++)
            {
                lbCountries.SetSelected(i, true);
            }
        }

        private void btnInfo_Click(object sender, EventArgs e)
        {
            var info = new Info();
            info.ShowDialog();
        }
    }
}
