using System.Collections.Generic;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Paged current-user request rows with quota summary.
/// </summary>
public class MyContentRequestsPagedResponse
{
    /// <summary>
    /// Gets or sets rows.
    /// </summary>
    public IReadOnlyList<ContentRequestRowDto> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets total count.
    /// </summary>
    public int TotalRecordCount { get; set; }

    /// <summary>
    /// Gets or sets quota summary.
    /// </summary>
    public ContentRequestCapSummaryDto Quota { get; set; } = new();
}
