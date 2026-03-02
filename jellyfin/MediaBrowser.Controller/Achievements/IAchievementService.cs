using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Achievements
{
    /// <summary>
    /// Handles achievement definitions, unlocks and history.
    /// </summary>
    public interface IAchievementService
    {
        /// <summary>
        /// Gets achievement definitions.
        /// </summary>
        /// <param name="includeSeasonal">A value indicating whether seasonal achievements should be included.</param>
        /// <returns>Achievement definitions.</returns>
        Task<IReadOnlyList<AchievementDefinitionInfo>> GetDefinitions(bool includeSeasonal);

        /// <summary>
        /// Gets unlock history for a user.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="take">The maximum rows to return.</param>
        /// <returns>User unlock history.</returns>
        Task<IReadOnlyList<UserAchievementInfo>> GetHistory(Guid userId, int take);

        /// <summary>
        /// Unlocks an achievement for a user if not already unlocked.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="achievementId">The achievement id.</param>
        /// <returns>The unlock result.</returns>
        Task<AchievementUnlockResult> Unlock(Guid userId, string achievementId);

        /// <summary>
        /// Evaluates all achievement milestones for the user and unlocks any newly met achievements.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <returns>Newly unlocked achievements for this sync run.</returns>
        Task<IReadOnlyList<UserAchievementInfo>> Sync(Guid userId);
    }
}
