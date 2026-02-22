using System;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Current subscription metadata response.
/// </summary>
public class CurrentSubscriptionResponse
{
    /// <summary>
    /// Gets or sets the user's current expiry date in UTC.
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Gets or sets the user's current status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the user is currently in grace period.
    /// </summary>
    public bool IsInGracePeriod { get; set; }

    /// <summary>
    /// Gets or sets remaining grace days.
    /// </summary>
    public int GraceDaysRemaining { get; set; }

    /// <summary>
    /// Gets or sets the most recently redeemed duration in months.
    /// </summary>
    public int? LastDurationMonths { get; set; }

    /// <summary>
    /// Gets or sets the most recent redemption date in UTC.
    /// </summary>
    public DateTime? LastRedeemedAt { get; set; }
}
