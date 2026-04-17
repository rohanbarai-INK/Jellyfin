using System;
using System.Collections.Generic;

namespace MediaBrowser.Controller.FeatureAnnouncements
{
    /// <summary>
    /// Announcement upsert request model.
    /// </summary>
    public class FeatureAnnouncementUpsertInfo
    {
        /// <summary>
        /// Gets or sets existing announcement id. Empty to create a new announcement.
        /// </summary>
        public Guid? Id { get; set; }

        /// <summary>
        /// Gets or sets campaign identifier.
        /// </summary>
        public string CampaignId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the announcement is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets announcement status.
        /// </summary>
        public FeatureAnnouncementStatus Status { get; set; }

        /// <summary>
        /// Gets or sets heading text.
        /// </summary>
        public string Heading { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets title text.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets subtitle text.
        /// </summary>
        public string Subtitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets body description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets highlights.
        /// </summary>
        public IReadOnlyList<string> Highlights { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets help text.
        /// </summary>
        public string HelpText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets hero gif source.
        /// </summary>
        public string HeroGifSource { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets media image source.
        /// </summary>
        public string MediaImageSource { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets media image alt text.
        /// </summary>
        public string MediaImageAlt { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets media image caption text.
        /// </summary>
        public string MediaImageCaption { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets CTA label.
        /// </summary>
        public string CtaLabel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets CTA target type.
        /// </summary>
        public FeatureAnnouncementCtaTargetType CtaTargetType { get; set; }

        /// <summary>
        /// Gets or sets CTA target value.
        /// </summary>
        public string CtaTarget { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets close label.
        /// </summary>
        public string CloseLabel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets campaign start time in UTC.
        /// </summary>
        public DateTime? StartsAtUtc { get; set; }

        /// <summary>
        /// Gets or sets campaign end time in UTC.
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
        /// Gets or sets priority.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets sort order.
        /// </summary>
        public int SortOrder { get; set; }
    }
}
