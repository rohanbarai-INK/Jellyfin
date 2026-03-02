using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Stores static achievement definitions used by the reward system.
    /// </summary>
    public class AchievementDefinition
    {
        /// <summary>
        /// Gets or sets the unique achievement id (kebab-case).
        /// </summary>
        [MaxLength(128)]
        [StringLength(128)]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the achievement title.
        /// </summary>
        [MaxLength(128)]
        [StringLength(128)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the achievement description.
        /// </summary>
        [MaxLength(512)]
        [StringLength(512)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the emoji used for the achievement image.
        /// </summary>
        [MaxLength(16)]
        [StringLength(16)]
        public string ImageEmoji { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the rarity.
        /// </summary>
        [MaxLength(16)]
        [StringLength(16)]
        public string Rarity { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the XP reward.
        /// </summary>
        public int Xp { get; set; }

        /// <summary>
        /// Gets or sets the coin reward.
        /// </summary>
        public int Coins { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this achievement is seasonal.
        /// </summary>
        public bool IsSeasonal { get; set; }
    }
}
