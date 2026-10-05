using System;

namespace iTunesMatchTagger
{
    public class UpdateOption
    {
        public string HeaderText { get; set; }
        public bool Update { get; set; }
        public bool Overwrite { get; set; }
        public string OverwriteValue { get; set; }
        public string LookupMember { get; set; }
        public string ITunesComMember { get; set; }
        public bool ShowOption { get; set; }
        public string NullValue { get; set; }

        public UpdateOption(string headerText, bool update, string lookupMember, string iTunesComMember, bool showOption = true, string nullValue = "")
        {
            HeaderText = headerText;
            Update = update;
            LookupMember = lookupMember;
            ITunesComMember = iTunesComMember;
            ShowOption = showOption;
            NullValue = nullValue;
        }
    }
}
