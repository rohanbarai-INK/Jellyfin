namespace MediaBrowser.Model.Configuration
{
    /// <summary>
    /// Configuration options for the homepage Trending Now rail.
    /// </summary>
    public class TrendingNowOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of slides/items returned for the homepage Trending rail.
        /// </summary>
        public int MaxSlides { get; set; } = 12;
    }
}
