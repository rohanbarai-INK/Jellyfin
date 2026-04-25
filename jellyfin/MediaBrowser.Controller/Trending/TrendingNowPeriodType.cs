namespace MediaBrowser.Controller.Trending
{
    /// <summary>
    /// Supported Trending Now time windows.
    /// </summary>
    public enum TrendingNowPeriodType
    {
        /// <summary>
        /// Current week.
        /// </summary>
        Week = 0,

        /// <summary>
        /// Current month.
        /// </summary>
        Month = 1,

        /// <summary>
        /// Current season.
        /// </summary>
        Season = 2
    }
}
