using System;
using System.Collections.Generic;

namespace MediaBrowser.Controller.FeatureAnnouncements
{
    /// <summary>
    /// Announcement configuration payload.
    /// </summary>
    public class FeatureAnnouncementInfo
    {
        /// <summary>
        /// Gets or sets the database identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the unique campaign identifier used by clients.
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
        /// Gets or sets announcement highlights.
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
        /// Gets or sets media caption text.
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
        /// Gets or sets priority used when multiple campaigns are active.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets sort order used for ordering admin list and slide position.
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
        public string CreatedByUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets updater user id.
        /// </summary>
        public Guid? UpdatedByUserId { get; set; }

        /// <summary>
        /// Gets or sets updater username.
        /// </summary>
        public string UpdatedByUsername { get; set; } = string.Empty;
    }
}
