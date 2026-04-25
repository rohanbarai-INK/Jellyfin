namespace MediaBrowser.Controller.PersonalInsights
{
    /// <summary>
    /// Library/category viewing distribution row for personal insights.
    /// </summary>
    public sealed class PersonalInsightsLibraryResult
    {
        /// <summary>
        /// Gets or sets the top-level library/category name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets validated watch minutes for the library/category.
        /// </summary>
        public double Minutes { get; set; }

        /// <summary>
        /// Gets or sets percentage share of validated watch time.
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Gets or sets the number of validated sessions contributing to this library/category.
        /// </summary>
        public int SessionCount { get; set; }

        /// <summary>
        /// Gets or sets the number of distinct titles contributing to this library/category.
        /// </summary>
        public int TitleCount { get; set; }
    }
}
