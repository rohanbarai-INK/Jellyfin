using System;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Admin response for a user's request quota.
/// </summary>
public class AdminContentRequestUserQuotaResponse
{
    /// <summary>
    /// Gets or sets user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets quota summary.
    /// </summary>
    public ContentRequestCapSummaryDto Quota { get; set; } = new();
}
