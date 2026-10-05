using System;
using System.Text;
using iTunesLib;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace iTunesMatchTagger
{
    public class Track : iTunesLookupResult
    {
        public IITTrack iTunesTrack;
        public long iTunesTrackId = 0;
        public bool LookupSuccess = false;

        public Track(IITTrack track, List<UpdateOption> options)
        {
            iTunesTrack = track;
            iTunesTrackId = GetTrackId();
            GetTagsFromTrack(options);
        }

        public string AlbumArtist { get; set; }

        public string Filename
        {
            get
            {
                try
                {
                    return ((dynamic)iTunesTrack).Location;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public void GetTagsFromTrack(List<UpdateOption> options)
        {
            foreach (var option in options.Where(x => x.ShowOption == true))
            {
                try
                {
                    var value = iTunesTrack.GetType().InvokeMember(option.ITunesComMember, System.Reflection.BindingFlags.GetProperty, null, iTunesTrack, null);
                    GetType().GetProperty(option.LookupMember).SetValue(this, value, null);
                }
                catch (Exception ex)
                {
                    string trackname = Filename != null ? Filename : trackName;
                    Logging.WriteLog(this, "unable to get " + option.HeaderText + " for " + trackName, Logging.Severity.Error);
                    Logging.WriteLog(this, ex.Message, Logging.Severity.Debug);
                }
            }
        }

        /// <summary>
        /// updates tags in iTunes
        /// </summary>
        /// <param name="track"></param>
        /// <param name="tags"></param>
        public void UpdateTrack(List<UpdateOption> options)
        {
            foreach (var option in options.Where(x => x.Update == true))
            {
                try
                {
                    if (option.Overwrite)
                    {
                        iTunesTrack.GetType().InvokeMember(option.ITunesComMember, System.Reflection.BindingFlags.SetProperty, null, iTunesTrack, new object[] { option.OverwriteValue });
                    }
                    else
                    {
                        var value = GetType().GetProperty(option.LookupMember).GetValue(this, null);
                        iTunesTrack.GetType().InvokeMember(option.ITunesComMember, System.Reflection.BindingFlags.SetProperty, null, iTunesTrack, new object[] { value });
                    }
                }
                catch (Exception ex)
                {
                    string trackname = Filename != null ? Filename : trackName;
                    Logging.WriteLog(this, "Unable to set " + option.HeaderText + " for '" + trackname + "'", Logging.Severity.Error);
                    Logging.WriteLog(this, ex.Message, Logging.Severity.Debug);
                }
            }

            GetTagsFromTrack(options);

            Logging.WriteLog(this, trackName + " updated", Logging.Severity.Debug);
        }

        public bool IsValid()
        {
            return (iTunesTrackId > 0);
        }

        public bool IsMatched()
        {
            return Settings.Default.MatchedKindAsString.Contains(iTunesTrack.KindAsString);
        }

        public bool IsDownloaded()
        {
            return Filename != null;
        }

        /// <summary>
        /// Opens Track-file and find iTunes-TrackId
        /// </summary>
        /// <param name="track">iTunes-Track</param>
        /// <returns>iTunes-TrackId</returns>
        protected long GetTrackId()
        {
            // only matched files
            if (!IsMatched() || !IsDownloaded()) return 0;

            try
            {
                Logging.WriteLog(this, "Get TrackId for " + Filename, Logging.Severity.Debug);

                byte[] byte_buffer = new byte[1024];
                FileStream f = new FileStream(Filename, FileMode.Open);
                f.Read(byte_buffer, 0, 1024);
                f.Close();
                string file_string = ASCIIEncoding.ASCII.GetString(byte_buffer);
                int pos = file_string.IndexOf("song");
                string hex = DecToHex(byte_buffer[pos + 4]) + DecToHex(byte_buffer[pos + 5]) +
                             DecToHex(byte_buffer[pos + 6]) + DecToHex(byte_buffer[pos + 7]);
                long id = HexToDec(hex);

                Logging.WriteLog(this, "iTunes-TrackId found: " + id, Logging.Severity.Debug);

                return id;
            }
            catch (Exception ex)
            {
                Logging.WriteLog(this, "GetTrackId() failed: " + ex.Message, Logging.Severity.Error);
                return 0;
            }
        }

        #region hex helper
        public static Int32 HexToDec(string hexValue)
        {
            return Int32.Parse(hexValue, System.Globalization.NumberStyles.HexNumber);
        }

        public static string DecToHex(int decValue)
        {
            return string.Format("{0:x2}", decValue);
        }
        #endregion
    }
}
