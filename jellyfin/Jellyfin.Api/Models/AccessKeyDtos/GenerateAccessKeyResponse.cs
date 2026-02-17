using System;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Generate access key response.
/// </summary>
public class GenerateAccessKeyResponse
{
    /// <summary>
    /// Gets or sets the generated key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the duration in months.
    /// </summary>
    public int DurationMonths { get; set; }

    /// <summary>
    /// Gets or sets the key creation date in UTC.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
