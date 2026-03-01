using System;
using System.ComponentModel.DataAnnotations;
using Jellyfin.Database.Implementations.Enums;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Stores pre-aggregated user watch ticks by genre and period.
    /// </summary>
    public class UserGenrePeriodStats
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
        /// Gets or sets the genre key.
        /// </summary>
        [MaxLength(128)]
        [StringLength(128)]
        public string GenreId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets total validated ticks for this genre in the period.
        /// </summary>
        public long TotalValidatedTicks { get; set; }
    }
}
