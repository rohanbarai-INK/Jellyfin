namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Admin subscription expiry radar buckets.
/// </summary>
public class AdminSubscriptionExpiryRadarResponse
{
    /// <summary>
    /// Gets or sets users expiring in next 24 hours.
    /// </summary>
    public int Next24h { get; set; }

    /// <summary>
    /// Gets or sets users expiring in next 3 days.
    /// </summary>
    public int Next3days { get; set; }

    /// <summary>
    /// Gets or sets users expiring in next 7 days.
    /// </summary>
    public int Next7days { get; set; }

    /// <summary>
    /// Gets or sets users expiring in next 30 days.
    /// </summary>
    public int Next30days { get; set; }
}
