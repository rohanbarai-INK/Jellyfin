namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Summary KPI payload.
    /// </summary>
    public sealed class PersonalInsightsSummaryDto
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
        /// Gets or sets movies watched.
        /// </summary>
        public int MoviesWatched { get; set; }

        /// <summary>
        /// Gets or sets movies delta.
        /// </summary>
        public int MoviesDelta { get; set; }

        /// <summary>
        /// Gets or sets episodes watched.
        /// </summary>
        public int EpisodesWatched { get; set; }

        /// <summary>
        /// Gets or sets episodes delta.
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
