using System;

namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Admin-facing Trending promotion payload.
    /// </summary>
    public sealed class TrendingPromotionDto
    {
        /// <summary>
        /// Gets or sets the database identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the external promotion identifier.
        /// </summary>
        public string PromotionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target item id.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets an optional item title for admin display.
        /// </summary>
        public string ItemTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the promotion is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the UTC start timestamp.
        /// </summary>
        public DateTime? StartsAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC end timestamp.
        /// </summary>
        public DateTime? EndsAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the optional explicit pin position.
        /// </summary>
        public int? PinPosition { get; set; }

        /// <summary>
        /// Gets or sets the additive boost amount.
        /// </summary>
        public double BoostAmount { get; set; }

        /// <summary>
        /// Gets or sets the audience segment.
        /// </summary>
        public string AudienceSegment { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional audience value.
        /// </summary>
        public string AudienceValue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the label override.
        /// </summary>
        public string LabelOverride { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tagline override.
        /// </summary>
        public string TaglineOverride { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the preferred artwork variant.
        /// </summary>
        public string ArtworkVariant { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets update timestamp in UTC.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets creator username.
        /// </summary>
        public string CreatedByUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets updater username.
        /// </summary>
        public string UpdatedByUsername { get; set; } = string.Empty;
    }
}
