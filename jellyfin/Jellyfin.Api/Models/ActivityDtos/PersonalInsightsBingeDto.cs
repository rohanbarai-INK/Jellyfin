using System.Collections.Generic;

namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Binge section payload.
    /// </summary>
    public sealed class PersonalInsightsBingeDto
    {
        /// <summary>
        /// Gets or sets longest binge streak.
        /// </summary>
        public int LongestStreak { get; set; }

        /// <summary>
        /// Gets or sets recent binge rows.
        /// </summary>
        public IReadOnlyList<PersonalInsightsRecentBingeDto> RecentBinges { get; set; } = [];
    }
}
