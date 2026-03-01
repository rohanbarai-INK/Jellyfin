using System;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Stores detected binge sessions for a user.
    /// </summary>
    public class UserBingeSession
    {
        /// <summary>
        /// Gets or sets the primary key.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user id.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the binge session date in UTC.
        /// </summary>
        public DateTime SessionDateUtc { get; set; }

        /// <summary>
        /// Gets or sets the series id.
        /// </summary>
        public Guid SeriesId { get; set; }

        /// <summary>
        /// Gets or sets the number of consecutive episodes in the binge.
        /// </summary>
        public int EpisodeCount { get; set; }

        /// <summary>
        /// Gets or sets total watch ticks represented by the binge.
        /// </summary>
        public long TotalWatchTicks { get; set; }
    }
}
