namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Admin subscription system health snapshot.
/// </summary>
public class AdminSubscriptionSystemHealthResponse
{
    /// <summary>
    /// Gets or sets rolling renewal rate percentage.
    /// </summary>
    public int RenewalRate { get; set; }

    /// <summary>
    /// Gets or sets active users count.
    /// </summary>
    public int ActiveUsers { get; set; }

    /// <summary>
    /// Gets or sets monthly growth percentage.
    /// </summary>
    public int MonthlyGrowth { get; set; }
}
