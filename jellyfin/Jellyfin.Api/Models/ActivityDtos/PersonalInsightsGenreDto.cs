namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Top genre row.
    /// </summary>
    public class PersonalInsightsGenreDto
    {
        /// <summary>
        /// Gets or sets genre name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets watched minutes.
        /// </summary>
        public double Minutes { get; set; }

        /// <summary>
        /// Gets or sets watched percentage.
        /// </summary>
        public double Percentage { get; set; }
    }
}
