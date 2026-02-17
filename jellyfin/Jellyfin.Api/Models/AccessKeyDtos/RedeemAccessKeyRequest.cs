using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Redeem access key request.
/// </summary>
public class RedeemAccessKeyRequest
{
    /// <summary>
    /// Gets or sets the key to redeem.
    /// </summary>
    [Required]
    public required string Key { get; set; }
}
