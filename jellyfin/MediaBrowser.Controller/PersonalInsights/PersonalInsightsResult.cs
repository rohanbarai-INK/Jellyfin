using System.Collections.Generic;

namespace MediaBrowser.Controller.PersonalInsights
{
    /// <summary>
    /// Aggregated personal-insights payload.
    /// </summary>
    public sealed class PersonalInsightsResult
    {
        /// <summary>
        /// Gets or sets summary metrics.
        /// </summary>
        public PersonalInsightsSummaryResult Summary { get; set; } = new();

        /// <summary>
        /// Gets or sets peak-viewing insights.
        /// </summary>
        public PersonalInsightsPeakViewingResult PeakViewing { get; set; } = new();

        /// <summary>
        /// Gets or sets continue-watching rows.
        /// </summary>
        public IReadOnlyList<PersonalInsightsContinueWatchingResult> ContinueWatching { get; set; } = [];

        /// <summary>
        /// Gets or sets binge insights.
        /// </summary>
        public PersonalInsightsBingeResult Binge { get; set; } = new();

        /// <summary>
        /// Gets or sets top genres.
        /// </summary>
        public IReadOnlyList<PersonalInsightsGenreResult> Genres { get; set; } = [];

        /// <summary>
        /// Gets or sets library/category watch distribution.
        /// </summary>
        public PersonalInsightsLibraryDistributionResult LibraryDistribution { get; set; } = new();

        /// <summary>
        /// Gets or sets generated insight text.
        /// </summary>
        public string InsightText { get; set; } = string.Empty;
    }
}
