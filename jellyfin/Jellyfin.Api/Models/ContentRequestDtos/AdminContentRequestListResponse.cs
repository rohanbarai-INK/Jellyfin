using System.Collections.Generic;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Paged admin request list response.
/// </summary>
public class AdminContentRequestListResponse
{
    /// <summary>
    /// Gets or sets rows.
    /// </summary>
    public IReadOnlyList<ContentRequestRowDto> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets total count.
    /// </summary>
    public int TotalRecordCount { get; set; }
}
