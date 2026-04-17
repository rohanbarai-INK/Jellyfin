using System;

namespace MediaBrowser.Controller.Leaderboard
{
    /// <summary>
    /// A single entry on the top leaderboard.
    /// </summary>
    public class LeaderboardEntryInfo
    {
        /// <summary>
        /// Gets or sets the user id.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the display name of the user.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the primary avatar image tag for the user.
        /// </summary>
        public string PrimaryImageTag { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the rank position (1-based).
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Gets or sets the total XP.
        /// </summary>
        public long TotalXp { get; set; }

        /// <summary>
        /// Gets or sets the level.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Gets or sets the achievement count.
        /// </summary>
        public int AchievementCount { get; set; }

        /// <summary>
        /// Gets or sets the rank title.
        /// </summary>
        public string RankTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the rank emoji.
        /// </summary>
        public string RankEmoji { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the badge kind shown for this row.
        /// </summary>
        public string BadgeKind { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this is the requesting user.
        /// </summary>
        public bool IsCurrentUser { get; set; }

        /// <summary>
        /// Gets or sets the metric value used for ranking in this leaderboard type.
        /// </summary>
        public long MetricValue { get; set; }

        /// <summary>
        /// Gets or sets the formatted display label for the metric value.
        /// </summary>
        public string MetricLabel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the secondary metric value shown for this row.
        /// </summary>
        public long SecondaryMetricValue { get; set; }

        /// <summary>
        /// Gets or sets the formatted label for the secondary metric.
        /// </summary>
        public string SecondaryMetricLabel { get; set; } = string.Empty;
    }
}
