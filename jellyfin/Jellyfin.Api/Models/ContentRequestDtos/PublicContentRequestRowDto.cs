using System;
using MediaBrowser.Controller.ContentRequests;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Public content request row DTO with requester identity removed.
/// </summary>
public class PublicContentRequestRowDto
{
    /// <summary>
    /// Gets or sets request id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets request type.
    /// </summary>
    public ContentRequestType Type { get; set; }

    /// <summary>
    /// Gets or sets season number for series requests.
    /// </summary>
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets request date.
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// Gets or sets request status.
    /// </summary>
    public ContentRequestStatus Status { get; set; }

    /// <summary>
    /// Gets or sets linked Jellyfin item id for completed requests.
    /// </summary>
    public Guid? JellyfinItemId { get; set; }
}
