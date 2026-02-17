using System;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Redeem access key response.
/// </summary>
public class RedeemAccessKeyResponse
{
    /// <summary>
    /// Gets or sets the user's updated expiry date.
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Gets or sets the duration in months that was redeemed.
    /// </summary>
    public int DurationMonths { get; set; }

    /// <summary>
    /// Gets or sets the redemption date in UTC.
    /// </summary>
    public DateTime RedeemedAt { get; set; }
}
