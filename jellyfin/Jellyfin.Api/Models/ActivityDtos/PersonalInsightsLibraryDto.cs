namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Library/category distribution row for personal insights.
    /// </summary>
    public sealed class PersonalInsightsLibraryDto
    {
        /// <summary>
        /// Gets or sets the library/category name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets validated watch minutes.
        /// </summary>
        public double Minutes { get; set; }

        /// <summary>
        /// Gets or sets percentage share.
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Gets or sets contributing session count.
        /// </summary>
        public int SessionCount { get; set; }

        /// <summary>
        /// Gets or sets contributing distinct title count.
        /// </summary>
        public int TitleCount { get; set; }
    }
}
