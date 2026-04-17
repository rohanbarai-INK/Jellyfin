using System;
using System.ComponentModel.DataAnnotations;
using Jellyfin.Database.Implementations.Enums;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Admin-managed announcement campaign entity.
    /// </summary>
    public class FeatureAnnouncement
    {
        /// <summary>
        /// Gets or sets primary key.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets unique campaign id consumed by clients.
        /// </summary>
        [MaxLength(128)]
        [StringLength(128)]
        public string CampaignId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether campaign is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets campaign status.
        /// </summary>
        public FeatureAnnouncementStatus Status { get; set; }

        /// <summary>
        /// Gets or sets heading text.
        /// </summary>
        [MaxLength(120)]
        [StringLength(120)]
        public string Heading { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets title text.
        /// </summary>
        [MaxLength(180)]
        [StringLength(180)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets subtitle text.
        /// </summary>
        [MaxLength(350)]
        [StringLength(350)]
        public string Subtitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets description text.
        /// </summary>
        [MaxLength(2000)]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets highlights serialized as JSON array.
        /// </summary>
        [MaxLength(4000)]
        [StringLength(4000)]
        public string HighlightsJson { get; set; } = "[]";

        /// <summary>
        /// Gets or sets help text.
        /// </summary>
        [MaxLength(1000)]
        [StringLength(1000)]
        public string HelpText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets hero gif source path or URL.
        /// </summary>
        [MaxLength(8192)]
        [StringLength(8192)]
        public string HeroGifSource { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets media image source path or URL.
        /// </summary>
        [MaxLength(5242880)]
        [StringLength(5242880)]
        public string MediaImageSource { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets media image alt text.
        /// </summary>
        [MaxLength(255)]
        [StringLength(255)]
        public string MediaImageAlt { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets media image caption text.
        /// </summary>
        [MaxLength(500)]
        [StringLength(500)]
        public string MediaImageCaption { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets CTA label.
        /// </summary>
        [MaxLength(100)]
        [StringLength(100)]
        public string CtaLabel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets CTA target type.
        /// </summary>
        public FeatureAnnouncementCtaTargetType CtaTargetType { get; set; }

        /// <summary>
        /// Gets or sets CTA target route or URL.
        /// </summary>
        [MaxLength(1024)]
        [StringLength(1024)]
        public string CtaTarget { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets close label.
        /// </summary>
        [MaxLength(100)]
        [StringLength(100)]
        public string CloseLabel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets campaign start timestamp in UTC.
        /// </summary>
        public DateTime? StartsAtUtc { get; set; }

        /// <summary>
        /// Gets or sets campaign end timestamp in UTC.
        /// </summary>
        public DateTime? EndsAtUtc { get; set; }

        /// <summary>
        /// Gets or sets max impressions per user per day.
        /// </summary>
        public int MaxImpressionsPerDay { get; set; }

        /// <summary>
        /// Gets or sets max total impressions per user.
        /// </summary>
        public int MaxImpressionsTotal { get; set; }

        /// <summary>
        /// Gets or sets campaign priority.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets sort order.
        /// </summary>
        public int SortOrder { get; set; }

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
