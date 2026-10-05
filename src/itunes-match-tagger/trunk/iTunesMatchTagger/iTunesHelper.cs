using System;
using System.Text;
using iTunesLib;
using System.Net;
using System.Collections.Specialized;
using System.Runtime.Serialization;

namespace iTunesMatchTagger
{
    public class iTunesHelper
    {
        private WebClient _web_client = new WebClient() { Encoding = Encoding.UTF8 }; // The search api under http://itunes.apple.com/lookup returns UTF8 encoded data.

        public static StringCollection StoreCountries = Settings.Default.StoreCountries;

        /// <summary>
        /// lookup track on iTunes
        /// </summary>
        /// <param name="track"></param>
        /// <param name="countries"></param>
        /// <returns></returns>
        public iTunesLookupResult LookupTrack(Track track, StringCollection countries)
        {
            foreach (var country in countries)
            {
                try
                {
                    iTunesLookupResult result = LookupTrack(track, country);
                    if (result != null)
                    {
                        iTunesLookup(track, result, country);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Logging.WriteLog(this, "LookupTrack failed: " + ex.Message, Logging.Severity.Error);
                }
            }
            iTunesLookup(track, null, "");
            return null;
        }

        /// <summary>
        /// lookup track on iTunes
        /// </summary>
        /// <param name="track"></param>
        /// <param name="country"></param>
        /// <returns></returns>
        public iTunesLookupResult LookupTrack(Track track, string country)
        {
            string json = _web_client.DownloadString("http://itunes.apple.com/lookup?id=" + track.iTunesTrackId + "&country=" + country);
            var search_result = JsonHelper.Deserialise<iTunesLookupResultCollection>(json);
            if (search_result.resultCount == 0)
            {
                return null;
            }
            if (search_result.resultCount > 1)
            {
                Logging.WriteLog(this, search_result.resultCount + " results found for " + track.Filename, Logging.Severity.Debug);
            }

            return search_result.results[0];
        }

        [DataContractAttribute]
        private class iTunesLookupResultCollection
        {
            [DataMemberAttribute]
            public int resultCount;
            [DataMemberAttribute]
            public iTunesLookupResult[] results;
        }

        #region lookup event
        public delegate void iTunesLookupEventHandler(object sender, iTunesLookupEventArgs args);

        public event iTunesLookupEventHandler OniTunesLookup;

        public class iTunesLookupEventArgs : EventArgs
        {
            public Track Track;
            public string Country;
            public iTunesLookupResult LookupResult;

            public iTunesLookupEventArgs(Track track, iTunesLookupResult lookupResult, string country)
            {
                Track = track;
                Country = country;
                LookupResult = lookupResult;
            }
        }

        private void iTunesLookup(Track track, iTunesLookupResult lookupResult, string country)
        {
            if (OniTunesLookup != null) OniTunesLookup(this, new iTunesLookupEventArgs(track, lookupResult, country));
        }
        #endregion
    }
}