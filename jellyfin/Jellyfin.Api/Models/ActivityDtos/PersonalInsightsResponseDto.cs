using System.Collections.Generic;

namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Personal-insights response payload.
    /// </summary>
    public sealed class PersonalInsightsResponseDto
    {
        /// <summary>
        /// Gets or sets summary metrics.
        /// </summary>
        public PersonalInsightsSummaryDto Summary { get; set; } = new();

        /// <summary>
        /// Gets or sets peak-viewing payload.
        /// </summary>
        public PersonalInsightsPeakViewingDto PeakViewing { get; set; } = new();

        /// <summary>
        /// Gets or sets continue-watching rows.
        /// </summary>
        public IReadOnlyList<PersonalInsightsContinueWatchingDto> ContinueWatching { get; set; } = [];

        /// <summary>
        /// Gets or sets binge payload.
        /// </summary>
        public PersonalInsightsBingeDto Binge { get; set; } = new();

        /// <summary>
        /// Gets or sets top genres.
        /// </summary>
        public IReadOnlyList<PersonalInsightsGenreDto> Genres { get; set; } = [];

        /// <summary>
        /// Gets or sets smart insight text.
        /// </summary>
        public string InsightText { get; set; } = string.Empty;
    }
}
