namespace MediaBrowser.Controller.Achievements
{
    /// <summary>
    /// Represents the outcome of an achievement unlock attempt.
    /// </summary>
    public sealed class AchievementUnlockResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the achievement was newly unlocked.
        /// </summary>
        public bool Unlocked { get; set; }

        /// <summary>
        /// Gets or sets the achievement payload.
        /// </summary>
        public UserAchievementInfo Achievement { get; set; } = new();
    }
}
