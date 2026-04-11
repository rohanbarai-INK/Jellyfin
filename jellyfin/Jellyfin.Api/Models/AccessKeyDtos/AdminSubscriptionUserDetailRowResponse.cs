using System;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Admin drilldown row for subscription state analytics.
/// </summary>
public class AdminSubscriptionUserDetailRowResponse
{
    /// <summary>
    /// Gets or sets the user id (N format).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expiry date (UTC), if any.
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Gets or sets the remaining days until expiry, when expiry date is in the future.
    /// </summary>
    public int? DaysRemaining { get; set; }

    /// <summary>
    /// Gets or sets the remaining grace days, when user is in grace.
    /// </summary>
    public int GraceDaysRemaining { get; set; }

    /// <summary>
    /// Gets or sets the plan label inferred from the latest redeemed key duration, when available.
    /// </summary>
    public string Plan { get; set; } = "N/A";

    /// <summary>
    /// Gets or sets the resolved state label.
    /// </summary>
    public string State { get; set; } = string.Empty;
}
