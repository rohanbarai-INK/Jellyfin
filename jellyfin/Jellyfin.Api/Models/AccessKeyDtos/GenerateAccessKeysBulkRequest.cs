using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Bulk generate access keys request.
/// </summary>
public class GenerateAccessKeysBulkRequest
{
    /// <summary>
    /// Gets or sets the duration in months for each key.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int DurationMonths { get; set; }

    /// <summary>
    /// Gets or sets the number of keys to generate.
    /// </summary>
    [Range(1, 1000)]
    public int Quantity { get; set; }
}
