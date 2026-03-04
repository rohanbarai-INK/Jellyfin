namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Admin subscription dashboard overview metrics.
/// </summary>
public class AdminSubscriptionOverviewResponse
{
    /// <summary>
    /// Gets or sets active users count.
    /// </summary>
    public int ActiveUsers { get; set; }

    /// <summary>
    /// Gets or sets grace users count.
    /// </summary>
    public int GraceUsers { get; set; }

    /// <summary>
    /// Gets or sets expired users count.
    /// </summary>
    public int ExpiredUsers { get; set; }

    /// <summary>
    /// Gets or sets users expiring in next 7 days.
    /// </summary>
    public int ExpiringSoon { get; set; }

    /// <summary>
    /// Gets or sets total redeemed revenue.
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// Gets or sets generated keys count.
    /// </summary>
    public int KeysGenerated { get; set; }

    /// <summary>
    /// Gets or sets redeemed keys count.
    /// </summary>
    public int KeysRedeemed { get; set; }

    /// <summary>
    /// Gets or sets unused keys count.
    /// </summary>
    public int UnusedKeys { get; set; }
}
