using System.Collections.Generic;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Paged public request response.
/// </summary>
public class PublicContentRequestListResponse
{
    /// <summary>
    /// Gets or sets rows.
    /// </summary>
    public IReadOnlyList<PublicContentRequestRowDto> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets total record count.
    /// </summary>
    public int TotalRecordCount { get; set; }
}
