using System.Collections.Generic;

namespace MediaBrowser.Controller.Leaderboard
{
    /// <summary>
    /// Result for the top leaderboard query.
    /// </summary>
    public class LeaderboardTopResult
    {
        /// <summary>
        /// Gets or sets the season year.
        /// </summary>
        public int SeasonYear { get; set; }

        /// <summary>
        /// Gets or sets the metric type for this leaderboard page.
        /// </summary>
        public string MetricType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the leaderboard entries.
        /// </summary>
        public IReadOnlyList<LeaderboardEntryInfo> Entries { get; set; } = [];

        /// <summary>
        /// Gets or sets the requesting user's position entry (if not in top list).
        /// </summary>
        public LeaderboardEntryInfo? CurrentUserPosition { get; set; }

        /// <summary>
        /// Gets or sets the total number of users in this season.
        /// </summary>
        public int TotalUsers { get; set; }

        /// <summary>
        /// Gets or sets the current offset for this page.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Gets or sets the requested page size.
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether more rows can be loaded.
        /// </summary>
        public bool HasMore { get; set; }
    }
}
