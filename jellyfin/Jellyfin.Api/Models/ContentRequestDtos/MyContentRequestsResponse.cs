using System.Collections.Generic;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// My requests response payload.
/// </summary>
public class MyContentRequestsResponse
{
    /// <summary>
    /// Gets or sets request rows.
    /// </summary>
    public IReadOnlyList<ContentRequestRowDto> Requests { get; set; } = [];

    /// <summary>
    /// Gets or sets cap summary.
    /// </summary>
    public ContentRequestCapSummaryDto Quota { get; set; } = new();
}
