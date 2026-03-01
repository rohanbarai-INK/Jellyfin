namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Hourly distribution row.
    /// </summary>
    public sealed class PersonalInsightsHourlyDistributionDto
    {
        /// <summary>
        /// Gets or sets hour (0..23).
        /// </summary>
        public int Hour { get; set; }

        /// <summary>
        /// Gets or sets watched minutes.
        /// </summary>
        public double Minutes { get; set; }
    }
}
