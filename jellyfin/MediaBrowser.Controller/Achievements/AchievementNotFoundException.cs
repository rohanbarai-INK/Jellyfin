using System;

namespace MediaBrowser.Controller.Achievements
{
    /// <summary>
    /// Thrown when an achievement id is not found.
    /// </summary>
    public sealed class AchievementNotFoundException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AchievementNotFoundException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        public AchievementNotFoundException(string message)
            : base(message)
        {
        }
    }
}
