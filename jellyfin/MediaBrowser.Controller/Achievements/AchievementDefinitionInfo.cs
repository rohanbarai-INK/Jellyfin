namespace MediaBrowser.Controller.Achievements
{
    /// <summary>
    /// Represents a static achievement definition.
    /// </summary>
    public sealed class AchievementDefinitionInfo
    {
        /// <summary>
        /// Gets or sets the achievement id.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the image emoji.
        /// </summary>
        public string ImageEmoji { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the rarity.
        /// </summary>
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
