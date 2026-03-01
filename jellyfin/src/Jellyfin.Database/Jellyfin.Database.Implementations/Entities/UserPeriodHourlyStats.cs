using System;
using Jellyfin.Database.Implementations.Enums;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Stores validated watch ticks by hour for a user period.
    /// </summary>
    public class UserPeriodHourlyStats
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
        public string PeriodKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hour bucket [0..23].
        /// </summary>
        public int Hour { get; set; }

        /// <summary>
        /// Gets or sets total validated ticks for this hour bucket.
        /// </summary>
        public long TotalValidatedTicks { get; set; }
    }
}
