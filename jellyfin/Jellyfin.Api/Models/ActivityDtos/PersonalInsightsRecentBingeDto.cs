namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Recent binge row.
    /// </summary>
    public sealed class PersonalInsightsRecentBingeDto
    {
        /// <summary>
        /// Gets or sets series name.
        /// </summary>
        public string SeriesName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets episode count.
        /// </summary>
        public int EpisodeCount { get; set; }
    }
}
