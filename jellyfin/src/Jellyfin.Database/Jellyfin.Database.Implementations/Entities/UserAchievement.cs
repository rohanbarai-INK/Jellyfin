using System;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Stores a user unlock record for a permanent or seasonal achievement.
    /// </summary>
    public class UserAchievement
    {
        /// <summary>
        /// Gets or sets the primary key.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user id.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the achievement id.
        /// </summary>
        [MaxLength(128)]
        [StringLength(128)]
        public string AchievementId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unlock timestamp in UTC.
        /// </summary>
        public DateTime UnlockedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the season year for seasonal unlocks. Null for permanent unlocks.
        /// </summary>
        public int? SeasonYear { get; set; }

        /// <summary>
        /// Gets or sets the associated user.
        /// </summary>
        public virtual User? User { get; set; }

        /// <summary>
        /// Gets or sets the associated achievement definition.
        /// </summary>
        public virtual AchievementDefinition? AchievementDefinition { get; set; }
    }
}
