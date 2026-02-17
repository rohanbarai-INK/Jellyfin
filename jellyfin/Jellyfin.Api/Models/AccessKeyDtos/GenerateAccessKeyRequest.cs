using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Generate access key request.
/// </summary>
public class GenerateAccessKeyRequest
{
    /// <summary>
    /// Gets or sets the duration in months to grant when redeemed.
    /// </summary>
    [Range(1, 12)]
    public int DurationMonths { get; set; }
}
