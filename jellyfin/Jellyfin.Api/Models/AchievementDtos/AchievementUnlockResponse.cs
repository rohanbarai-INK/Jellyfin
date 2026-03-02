namespace Jellyfin.Api.Models.AchievementDtos;

/// <summary>
/// Unlock achievement response payload.
/// </summary>
public class AchievementUnlockResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the achievement was newly unlocked.
    /// </summary>
    public bool Unlocked { get; set; }

    /// <summary>
    /// Gets or sets the achievement payload.
    /// </summary>
    public UserAchievementDto Achievement { get; set; } = new();
}
