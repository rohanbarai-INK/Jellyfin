using System;
using System.ComponentModel.DataAnnotations;
using Jellyfin.Database.Implementations.Enums;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Stores pre-aggregated user watch insights by period.
    /// </summary>
    public class UserPeriodStats
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
        /// Gets or sets the period type.
        /// </summary>
        public PeriodType PeriodType { get; set; }

        /// <summary>
        /// Gets or sets the period key.
        /// </summary>
        [MaxLength(16)]
        [StringLength(16)]
        public string PeriodKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC period start.
        /// </summary>
        public DateTime PeriodStartUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC period end.
        /// </summary>
        public DateTime PeriodEndUtc { get; set; }

        /// <summary>
        /// Gets or sets total validated ticks for the period.
        /// </summary>
        public long TotalValidatedTicks { get; set; }

        /// <summary>
        /// Gets or sets the watch-session count.
        /// </summary>
        public int SessionCount { get; set; }

        /// <summary>
        /// Gets or sets completed movie count.
        /// </summary>
        public int CompletedMovies { get; set; }

        /// <summary>
        /// Gets or sets completed episode count.
        /// </summary>
        public int CompletedEpisodes { get; set; }

        /// <summary>
        /// Gets or sets binge-session count.
        /// </summary>
        public int BingeSessions { get; set; }
    }
}
