using System;
using MediaBrowser.Controller.ContentRequests;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Content request row DTO.
/// </summary>
public class ContentRequestRowDto
{
    /// <summary>
    /// Gets or sets request id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets owner user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets owner username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets request type.
    /// </summary>
    public ContentRequestType Type { get; set; }

    /// <summary>
    /// Gets or sets requested season number.
    /// </summary>
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets created timestamp.
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// Gets or sets request status.
    /// </summary>
    public ContentRequestStatus Status { get; set; }

    /// <summary>
    /// Gets or sets linked Jellyfin item id.
    /// </summary>
    public Guid? JellyfinItemId { get; set; }

    /// <summary>
    /// Gets or sets notification view count.
    /// </summary>
    public int NotificationCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether admin has viewed this pending row.
    /// </summary>
    public bool IsAdminViewed { get; set; }
}
