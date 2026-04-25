using System.Collections.Generic;

namespace MediaBrowser.Controller.PersonalInsights
{
    /// <summary>
    /// Library/category watch distribution payload for personal insights.
    /// </summary>
    public sealed class PersonalInsightsLibraryDistributionResult
    {
        /// <summary>
        /// Gets or sets distribution rows.
        /// </summary>
        public IReadOnlyList<PersonalInsightsLibraryResult> Libraries { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether there is any library/category viewing activity.
        /// </summary>
        public bool HasViewingActivity { get; set; }

        /// <summary>
        /// Gets or sets human-readable summary text.
        /// </summary>
        public string InsightText { get; set; } = string.Empty;
    }
}
