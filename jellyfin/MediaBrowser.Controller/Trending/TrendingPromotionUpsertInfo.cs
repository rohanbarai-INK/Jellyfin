using System;

namespace MediaBrowser.Controller.Trending
{
    /// <summary>
    /// Upsert payload for Trending Now admin promotions.
    /// </summary>
    public class TrendingPromotionUpsertInfo
    {
        /// <summary>
        /// Gets or sets an existing promotion id. Empty to create a new promotion.
        /// </summary>
        public Guid? Id { get; set; }

        /// <summary>
        /// Gets or sets the external promotion identifier.
        /// </summary>
        public string PromotionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target item id.
        /// </summary>
        public Guid ItemId { get; set; }

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
        public TrendingAudienceSegment AudienceSegment { get; set; }

        /// <summary>
        /// Gets or sets an optional audience segment value such as a genre.
        /// </summary>
        public string AudienceValue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional label override.
        /// </summary>
        public string LabelOverride { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional hero tagline override.
        /// </summary>
        public string TaglineOverride { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional preferred artwork variant key.
        /// </summary>
        public string ArtworkVariant { get; set; } = string.Empty;
    }
}
