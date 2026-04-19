using System;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Admin user suggestion row for request reward assignment.
/// </summary>
public class AdminContentRequestUserSuggestionDto
{
    /// <summary>
    /// Gets or sets user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets username.
    /// </summary>
    public string Username { get; set; } = string.Empty;
}
