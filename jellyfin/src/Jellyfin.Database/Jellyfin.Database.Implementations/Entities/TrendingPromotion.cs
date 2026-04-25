using System;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Admin-managed promotion for the unified Trending Now rail.
    /// </summary>
    public class TrendingPromotion
    {
        /// <summary>
        /// Gets or sets primary key.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets unique promotion identifier consumed by clients/admins.
        /// </summary>
        [MaxLength(128)]
        [StringLength(128)]
        public string PromotionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets target content item id.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the promotion is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets campaign start timestamp in UTC.
        /// </summary>
        public DateTime? StartsAtUtc { get; set; }

        /// <summary>
        /// Gets or sets campaign end timestamp in UTC.
        /// </summary>
        public DateTime? EndsAtUtc { get; set; }

        /// <summary>
        /// Gets or sets optional explicit pin position.
        /// </summary>
        public int? PinPosition { get; set; }

        /// <summary>
        /// Gets or sets additive score boost.
        /// </summary>
        public double BoostAmount { get; set; }

        /// <summary>
        /// Gets or sets audience segment key.
        /// </summary>
        [MaxLength(64)]
        [StringLength(64)]
        public string AudienceSegment { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets optional audience value such as a genre.
        /// </summary>
        [MaxLength(128)]
        [StringLength(128)]
        public string AudienceValue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets label override.
        /// </summary>
        [MaxLength(120)]
        [StringLength(120)]
        public string LabelOverride { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets tagline override.
        /// </summary>
        [MaxLength(350)]
        [StringLength(350)]
        public string TaglineOverride { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets optional artwork variant key.
        /// </summary>
        [MaxLength(120)]
        [StringLength(120)]
        public string ArtworkVariant { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets latest update timestamp in UTC.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets creator user id.
        /// </summary>
        public Guid? CreatedByUserId { get; set; }

        /// <summary>
        /// Gets or sets creator username.
        /// </summary>
        [MaxLength(255)]
        [StringLength(255)]
        public string CreatedByUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets updater user id.
        /// </summary>
        public Guid? UpdatedByUserId { get; set; }

        /// <summary>
        /// Gets or sets updater username.
        /// </summary>
        [MaxLength(255)]
        [StringLength(255)]
        public string UpdatedByUsername { get; set; } = string.Empty;
    }
}
