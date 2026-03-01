namespace MediaBrowser.Controller.PersonalInsights
{
    /// <summary>
    /// Hourly watch-time distribution point.
    /// </summary>
    public sealed class PersonalInsightsHourlyDistributionResult
    {
        /// <summary>
        /// Gets or sets hour (0..23).
        /// </summary>
        public int Hour { get; set; }

        /// <summary>
        /// Gets or sets watch minutes.
        /// </summary>
        public double Minutes { get; set; }
    }
}
