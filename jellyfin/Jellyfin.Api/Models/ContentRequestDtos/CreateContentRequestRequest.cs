using System.ComponentModel.DataAnnotations;
using MediaBrowser.Controller.ContentRequests;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Create content request payload.
/// </summary>
public class CreateContentRequestRequest
{
    /// <summary>
    /// Gets or sets the requested title.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets request type.
    /// </summary>
    [Required]
    public ContentRequestType Type { get; set; }

    /// <summary>
    /// Gets or sets requested season number for series.
    /// </summary>
    public int? SeasonNumber { get; set; }
}
