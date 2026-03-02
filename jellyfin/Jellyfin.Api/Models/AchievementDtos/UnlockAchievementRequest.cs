using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.AchievementDtos;

/// <summary>
/// Unlock achievement request payload.
/// </summary>
public class UnlockAchievementRequest
{
    /// <summary>
    /// Gets or sets the achievement id.
    /// </summary>
    [Required]
    public string AchievementId { get; set; } = string.Empty;
}
