using System;
using System.Collections.Generic;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Bulk generate access keys response.
/// </summary>
public class GenerateAccessKeysBulkResponse
{
    /// <summary>
    /// Gets or sets generated key rows.
    /// </summary>
    public IReadOnlyList<GenerateAccessKeyResponse> Items { get; set; } = Array.Empty<GenerateAccessKeyResponse>();
}
