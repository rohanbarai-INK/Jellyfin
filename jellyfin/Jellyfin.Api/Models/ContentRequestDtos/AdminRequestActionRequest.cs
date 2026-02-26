using System;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Admin action payload for request status changes that only require request id.
/// </summary>
public class AdminRequestActionRequest
{
    /// <summary>
    /// Gets or sets request id.
    /// </summary>
    [Required]
    public Guid RequestId { get; set; }
}
