using System;
using System.Collections.Generic;

namespace Jellyfin.Api.Models.FeatureAnnouncementDtos;

/// <summary>
/// Feature announcement payload for admin and active API responses.
/// </summary>
public class FeatureAnnouncementDto
{
    /// <summary>
    /// Gets or sets announcement id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets campaign id used in clients.
    /// </summary>
    public string CampaignId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether campaign is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets status string (Draft/Published).
    /// </summary>
    public string Status { get; set; } = string.Empty;

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
    /// Gets or sets description text.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets highlight strings.
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
    /// Gets or sets media image caption.
    /// </summary>
    public string MediaImageCaption { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets CTA label.
    /// </summary>
    public string CtaLabel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets CTA target type string (InternalRoute/ExternalUrl).
    /// </summary>
    public string CtaTargetType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets CTA target value.
    /// </summary>
    public string CtaTarget { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets close label.
    /// </summary>
    public string CloseLabel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets start time in UTC.
    /// </summary>
    public DateTime? StartsAtUtc { get; set; }

    /// <summary>
    /// Gets or sets end time in UTC.
    /// </summary>
    public DateTime? EndsAtUtc { get; set; }

    /// <summary>
    /// Gets or sets max impressions per day.
    /// </summary>
    public int MaxImpressionsPerDay { get; set; }

    /// <summary>
    /// Gets or sets max total impressions.
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

    /// <summary>
    /// Gets or sets created timestamp in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets updated timestamp in UTC.
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
