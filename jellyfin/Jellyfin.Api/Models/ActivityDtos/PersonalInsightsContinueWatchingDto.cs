using System;

namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Continue-watching row.
    /// </summary>
    public class PersonalInsightsContinueWatchingDto
    {
        /// <summary>
        /// Gets or sets item id.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets title.
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
