namespace MediaBrowser.Controller.PersonalInsights
{
    /// <summary>
    /// Summary KPI values for personal insights.
    /// </summary>
    public sealed class PersonalInsightsSummaryResult
    {
        /// <summary>
        /// Gets or sets total watch hours.
        /// </summary>
        public double TotalWatchHours { get; set; }

        /// <summary>
        /// Gets or sets watch-time change percent.
        /// </summary>
        public double WatchTimeChangePercent { get; set; }

        /// <summary>
        /// Gets or sets completed movies in period.
        /// </summary>
        public int MoviesWatched { get; set; }

        /// <summary>
        /// Gets or sets movie delta versus previous period.
        /// </summary>
        public int MoviesDelta { get; set; }

        /// <summary>
        /// Gets or sets completed episodes in period.
        /// </summary>
        public int EpisodesWatched { get; set; }

        /// <summary>
        /// Gets or sets episode delta versus previous period.
        /// </summary>
        public int EpisodesDelta { get; set; }

        /// <summary>
        /// Gets or sets engagement score label.
        /// </summary>
        public string EngagementScore { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets engagement percentile.
        /// </summary>
        public int EngagementPercentile { get; set; }
    }
}
