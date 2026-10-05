
namespace iTunesMatchTagger
{
    public class iTunesLookupResult
    {
        public string wrapperType { get; set; }
        public string kind { get; set; }
        public long? artistId { get; set; }
        public long? collectionId { get; set; }
        public long? trackId { get; set; }
        public string artistName { get; set; }
        public string collectionName { get; set; }
        public string trackName { get; set; }
        public string collectionCensoredName { get; set; }
        public string trackCensoredName { get; set; }
        public string artistViewUrl { get; set; }
        public string collectionViewUrl { get; set; }
        public string trackViewUrl { get; set; }
        public string previewUrl { get; set; }
        public string artworkUrl30 { get; set; }
        public string artworkUrl60 { get; set; }
        public string artworkUrl100 { get; set; }
        public float? collectionPrice { get; set; }
        public float? trackPrice { get; set; }
        public string releaseDate { get; set; }
        public string collectionExplicitness { get; set; }
        public string trackExplicitness { get; set; }
        public int? discCount { get; set; }
        public int? discNumber { get; set; }
        public int? trackCount { get; set; }
        public int? trackNumber { get; set; }
        public long? trackTimeMillis { get; set; }
        public string country { get; set; }
        public string currency { get; set; }
        public string primaryGenreName { get; set; }

        public int? year
        {
            get
            {
                if (string.IsNullOrEmpty(releaseDate) || releaseDate.Length < 4) return null;
                return int.Parse(releaseDate.Substring(0, 4));
            }
            set
            {
                if (releaseDate == null || releaseDate.Length < 4) releaseDate = value.ToString();
                else releaseDate = value + releaseDate.Substring(4);
            }
        }
    }
}
