using System;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Stores validated watch-session telemetry for server-authoritative analytics.
    /// </summary>
    public class UserWatchSession
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
        /// Gets or sets the item id.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the playback session id.
        /// </summary>
        [MaxLength(128)]
        [StringLength(128)]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC start timestamp.
        /// </summary>
        public DateTime StartTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC end timestamp.
        /// </summary>
        public DateTime? EndTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets the raw accumulated ticks reported by playback progression.
        /// </summary>
        public long AccumulatedTicks { get; set; }

        /// <summary>
        /// Gets or sets the validated ticks accepted after anti-abuse filtering.
        /// </summary>
        public long ValidatedTicks { get; set; }

        /// <summary>
        /// Gets or sets the observed playback speed.
        /// </summary>
        public double PlaybackSpeed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this session is valid for aggregation.
        /// </summary>
        public bool IsValidSession { get; set; } = true;

        /// <summary>
        /// Gets or sets the suspicion score assigned during validation.
        /// </summary>
        public int SuspicionScore { get; set; }
    }
}
