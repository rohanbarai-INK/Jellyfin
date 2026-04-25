using System;

namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Upsert request for Trending promotions.
    /// </summary>
    public sealed class UpsertTrendingPromotionRequest
    {
        /// <summary>
        /// Gets or sets the existing database id. Empty to create a new row.
        /// </summary>
        public Guid? Id { get; set; }

        /// <summary>
        /// Gets or sets the external promotion id.
        /// </summary>
        public string PromotionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target item id.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the row is enabled.
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
    }
}
