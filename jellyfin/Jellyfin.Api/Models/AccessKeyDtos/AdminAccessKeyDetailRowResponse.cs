using System;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Admin drilldown row for access key analytics.
/// </summary>
public class AdminAccessKeyDetailRowResponse
{
    /// <summary>
    /// Gets or sets the access key string.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key duration in months.
    /// </summary>
    public int DurationMonths { get; set; }

    /// <summary>
    /// Gets or sets when the key was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the key has been redeemed.
    /// </summary>
    public bool IsRedeemed { get; set; }

    /// <summary>
    /// Gets or sets when the key was redeemed (UTC), if redeemed.
    /// </summary>
    public DateTime? RedeemedAt { get; set; }

    /// <summary>
    /// Gets or sets the user id (N format) that redeemed this key, if redeemed.
    /// </summary>
    public string? RedeemedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the username that redeemed this key, if available.
    /// </summary>
    public string? RedeemedByUsername { get; set; }

    /// <summary>
    /// Gets or sets the billed amount captured at redemption time, if redeemed.
    /// </summary>
    public decimal? RedeemedAmount { get; set; }

    /// <summary>
    /// Gets or sets the applied billing cycle start date (UTC), if available.
    /// </summary>
    public DateTime? CycleStartDate { get; set; }

    /// <summary>
    /// Gets or sets the applied billing cycle end date (UTC), if available.
    /// </summary>
    public DateTime? CycleEndDate { get; set; }
}
