using System;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Admin subscription expiring user row.
/// </summary>
public class AdminSubscriptionExpiringUserResponse
{
    /// <summary>
    /// Gets or sets user id.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Gets or sets username.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Gets or sets expiry date in UTC.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// Gets or sets remaining days.
    /// </summary>
    public int DaysRemaining { get; set; }

    /// <summary>
    /// Gets or sets latest plan label.
    /// </summary>
    public required string Plan { get; set; }
}
