using System;
using System.Collections.Generic;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Billing history response.
/// </summary>
public class BillingHistoryResponse
{
    /// <summary>
    /// Gets or sets the billing history records.
    /// </summary>
    public IReadOnlyList<BillingHistoryEntryResponse> Items { get; set; } = Array.Empty<BillingHistoryEntryResponse>();
}
