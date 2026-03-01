namespace MediaBrowser.Controller.PersonalInsights
{
    /// <summary>
    /// Recent binge row.
    /// </summary>
    public sealed class PersonalInsightsRecentBingeResult
    {
        /// <summary>
        /// Gets or sets series name.
        /// </summary>
        public string SeriesName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets episode count.
        /// </summary>
        public int EpisodeCount { get; set; }
    }
}
