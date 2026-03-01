namespace MediaBrowser.Controller.PersonalInsights
{
    /// <summary>
    /// Top genre insight row.
    /// </summary>
    public sealed class PersonalInsightsGenreResult
    {
        /// <summary>
        /// Gets or sets the genre name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the watched minutes.
        /// </summary>
        public double Minutes { get; set; }

        /// <summary>
        /// Gets or sets the watched percentage.
        /// </summary>
        public double Percentage { get; set; }
    }
}
