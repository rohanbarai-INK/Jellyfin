namespace MediaBrowser.Controller.Trending
{
    /// <summary>
    /// Simple admin target segments for Trending Now promotions.
    /// </summary>
    public enum TrendingAudienceSegment
    {
        /// <summary>
        /// Promotion applies to all signed-in users.
        /// </summary>
        AllUsers = 0,

        /// <summary>
        /// Promotion applies to users with little or no viewing history.
        /// </summary>
        NewOrLowHistory = 1,

        /// <summary>
        /// Promotion applies to users with established viewing history.
        /// </summary>
        ReturningUsers = 2,

        /// <summary>
        /// Promotion applies to movie-leaning viewers.
        /// </summary>
        MovieHeavy = 3,

        /// <summary>
        /// Promotion applies to series-leaning viewers.
        /// </summary>
        SeriesHeavy = 4,

        /// <summary>
        /// Promotion applies when the user's top genres contain the configured genre value.
        /// </summary>
        TopGenreMatch = 5
    }
}
