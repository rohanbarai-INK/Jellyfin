using System.Collections.Generic;

namespace MediaBrowser.Controller.PersonalInsights
{
    /// <summary>
    /// Binge insights payload.
    /// </summary>
    public sealed class PersonalInsightsBingeResult
    {
        /// <summary>
        /// Gets or sets longest streak.
        /// </summary>
        public int LongestStreak { get; set; }

        /// <summary>
        /// Gets or sets recent binge rows.
        /// </summary>
        public IReadOnlyList<PersonalInsightsRecentBingeResult> RecentBinges { get; set; } = [];
    }
}
