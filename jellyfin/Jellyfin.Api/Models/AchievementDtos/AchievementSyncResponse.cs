using System.Collections.Generic;

namespace Jellyfin.Api.Models.AchievementDtos
{
    /// <summary>
    /// Response payload for a milestone sync run.
    /// </summary>
    public class AchievementSyncResponse
    {
        /// <summary>
        /// Gets or sets newly unlocked achievements.
        /// </summary>
        public IReadOnlyList<UserAchievementDto> UnlockedAchievements { get; set; } = [];
    }
}
