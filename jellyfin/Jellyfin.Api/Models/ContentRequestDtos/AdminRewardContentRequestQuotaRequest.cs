using System;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Admin payload for granting rewarded request quota to a user.
/// </summary>
public class AdminRewardContentRequestQuotaRequest
{
    /// <summary>
    /// Gets or sets target user id.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets movie slots to add.
    /// </summary>
    public int MovieCount { get; set; }

    /// <summary>
    /// Gets or sets series slots to add.
    /// </summary>
    public int SeriesCount { get; set; }
}
