using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Browser push unsubscription payload.
/// </summary>
public class RequestWebPushUnsubscribeRequest
{
    /// <summary>
    /// Gets or sets the subscription endpoint.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string Endpoint { get; set; } = string.Empty;
}
