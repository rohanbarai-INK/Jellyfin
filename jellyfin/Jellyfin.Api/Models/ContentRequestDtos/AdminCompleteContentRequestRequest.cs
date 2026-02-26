using System;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Admin complete payload.
/// </summary>
public class AdminCompleteContentRequestRequest
{
    /// <summary>
    /// Gets or sets request id.
    /// </summary>
    [Required]
    public Guid RequestId { get; set; }

    /// <summary>
    /// Gets or sets Jellyfin item id linked to the completed request.
    /// </summary>
    [Required]
    public Guid JellyfinItemId { get; set; }
}
