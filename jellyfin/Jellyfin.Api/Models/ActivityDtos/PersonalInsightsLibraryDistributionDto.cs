using System.Collections.Generic;

namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Library/category watch distribution payload for personal insights.
    /// </summary>
    public sealed class PersonalInsightsLibraryDistributionDto
    {
        /// <summary>
        /// Gets or sets distribution rows.
        /// </summary>
        public IReadOnlyList<PersonalInsightsLibraryDto> Libraries { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether there is any viewing activity.
        /// </summary>
        public bool HasViewingActivity { get; set; }

        /// <summary>
        /// Gets or sets human-readable summary text.
        /// </summary>
        public string InsightText { get; set; } = string.Empty;
    }
}
