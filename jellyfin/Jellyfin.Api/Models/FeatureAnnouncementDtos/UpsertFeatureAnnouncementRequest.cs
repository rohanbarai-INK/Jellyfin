using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.FeatureAnnouncementDtos;

/// <summary>
/// Admin request payload for creating or updating a feature announcement.
/// </summary>
public class UpsertFeatureAnnouncementRequest
{
    /// <summary>
    /// Gets or sets announcement id. Empty to create.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets campaign id.
    /// </summary>
    public string CampaignId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether campaign is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets status string (Draft/Published).
    /// </summary>
    [Required]
    public string Status { get; set; } = "Draft";

    /// <summary>
    /// Gets or sets heading text.
    /// </summary>
    public string Heading { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets title text.
    /// </summary>
    [Required]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets subtitle text.
    /// </summary>
    public string Subtitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets description text.
    /// </summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets highlights.
    /// </summary>
    public IReadOnlyList<string>? Highlights { get; set; }

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
    [Required]
    public string CtaTargetType { get; set; } = "InternalRoute";

    /// <summary>
    /// Gets or sets CTA target route or URL.
    /// </summary>
    [Required]
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
    /// Gets or sets max impressions per user per day.
    /// </summary>
    public int MaxImpressionsPerDay { get; set; } = 2;

    /// <summary>
    /// Gets or sets max total impressions per user.
    /// </summary>
    public int MaxImpressionsTotal { get; set; } = 10;

    /// <summary>
    /// Gets or sets priority.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets sort order.
    /// </summary>
    public int SortOrder { get; set; }
}
