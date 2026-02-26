using System;
using System.Collections.Generic;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Request ids for notification viewed bulk updates.
/// </summary>
public class BulkNotificationViewedRequest
{
    /// <summary>
    /// Gets or sets request ids to mark viewed.
    /// </summary>
    public IReadOnlyList<Guid> RequestIds { get; set; } = [];
}
