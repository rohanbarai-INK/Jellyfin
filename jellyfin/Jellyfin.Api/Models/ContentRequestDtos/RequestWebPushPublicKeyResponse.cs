namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Browser push public key payload.
/// </summary>
public class RequestWebPushPublicKeyResponse
{
    /// <summary>
    /// Gets or sets the VAPID public key.
    /// </summary>
    public string PublicKey { get; set; } = string.Empty;
}
