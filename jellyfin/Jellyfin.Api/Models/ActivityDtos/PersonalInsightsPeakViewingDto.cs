using System.Collections.Generic;

namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Peak viewing payload.
    /// </summary>
    public sealed class PersonalInsightsPeakViewingDto
    {
        /// <summary>
        /// Gets or sets hourly distribution.
        /// </summary>
        public IReadOnlyList<PersonalInsightsHourlyDistributionDto> HourlyDistribution { get; set; } = [];

        /// <summary>
        /// Gets or sets peak hour.
        /// </summary>
        public int PeakHour { get; set; }

        /// <summary>
        /// Gets or sets descriptive label.
        /// </summary>
        public string Label { get; set; } = string.Empty;
    }
}
