using System.Collections.Generic;

namespace MediaBrowser.Controller.PersonalInsights
{
    /// <summary>
    /// Peak-viewing data for personal insights.
    /// </summary>
    public sealed class PersonalInsightsPeakViewingResult
    {
        /// <summary>
        /// Gets or sets hourly distribution values.
        /// </summary>
        public IReadOnlyList<PersonalInsightsHourlyDistributionResult> HourlyDistribution { get; set; } = [];

        /// <summary>
        /// Gets or sets peak hour (0..23).
        /// </summary>
        public int PeakHour { get; set; }

        /// <summary>
        /// Gets or sets descriptive label.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the selected period has viewing activity.
        /// </summary>
        public bool HasViewingActivity { get; set; }
    }
}
