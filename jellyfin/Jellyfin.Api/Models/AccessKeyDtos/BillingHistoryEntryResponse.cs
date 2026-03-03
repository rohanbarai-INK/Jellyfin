using System;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// A single subscription billing history record.
/// </summary>
public class BillingHistoryEntryResponse
{
    /// <summary>
    /// Gets or sets the billing reference identifier.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the redeemed duration in months.
    /// </summary>
    public int DurationMonths { get; set; }

    /// <summary>
    /// Gets or sets the billing cycle start date in UTC.
    /// </summary>
    public DateTime CycleStartDate { get; set; }

    /// <summary>
    /// Gets or sets the billing cycle end date in UTC.
    /// </summary>
    public DateTime CycleEndDate { get; set; }

    /// <summary>
    /// Gets or sets the redemption date in UTC.
    /// </summary>
    public DateTime RedeemedAt { get; set; }

    /// <summary>
    /// Gets or sets the immutable billed amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the billing status.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
