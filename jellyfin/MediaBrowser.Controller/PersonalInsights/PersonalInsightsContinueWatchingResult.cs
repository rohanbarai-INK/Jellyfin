using System;

namespace MediaBrowser.Controller.PersonalInsights
{
    /// <summary>
    /// Continue-watching row.
    /// </summary>
    public sealed class PersonalInsightsContinueWatchingResult
    {
        /// <summary>
        /// Gets or sets the item id.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets series name.
        /// </summary>
        public string SeriesName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets season number.
        /// </summary>
        public int? SeasonNumber { get; set; }

        /// <summary>
        /// Gets or sets episode number.
        /// </summary>
        public int? EpisodeNumber { get; set; }

        /// <summary>
        /// Gets or sets remaining minutes.
        /// </summary>
        public double RemainingMinutes { get; set; }

        /// <summary>
        /// Gets or sets image URL.
        /// </summary>
        public string ImageUrl { get; set; } = string.Empty;
    }
}
