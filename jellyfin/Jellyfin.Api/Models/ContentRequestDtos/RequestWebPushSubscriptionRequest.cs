using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Browser push subscription payload.
/// </summary>
public class RequestWebPushSubscriptionRequest
{
    /// <summary>
    /// Gets or sets the subscription endpoint.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the p256dh key.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string P256dh { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the auth key.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string Auth { get; set; } = string.Empty;
}
