namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Admin subscription key metrics.
/// </summary>
public class AdminSubscriptionKeyStatsResponse
{
    /// <summary>
    /// Gets or sets total generated keys.
    /// </summary>
    public int TotalGenerated { get; set; }

    /// <summary>
    /// Gets or sets total redeemed keys.
    /// </summary>
    public int Redeemed { get; set; }

    /// <summary>
    /// Gets or sets unused keys.
    /// </summary>
    public int Unused { get; set; }

    /// <summary>
    /// Gets or sets expired cycles across redeemed keys.
    /// </summary>
    public int Expired { get; set; }
}
