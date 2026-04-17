using System;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Stores cached seasonal XP totals per user for leaderboard ranking.
    /// </summary>
    public class UserSeasonStats
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
        /// Gets or sets the season year (e.g. 2025).
        /// </summary>
        public int SeasonYear { get; set; }

        /// <summary>
        /// Gets or sets the total XP earned in this season (achievements + activity).
        /// </summary>
        public long TotalXp { get; set; }

        /// <summary>
        /// Gets or sets the XP earned from achievements in this season.
        /// </summary>
        public long AchievementXp { get; set; }

        /// <summary>
        /// Gets or sets the number of achievements unlocked in this season.
        /// </summary>
        public int AchievementCount { get; set; }

        /// <summary>
        /// Gets or sets the computed level from <see cref="TotalXp"/>.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Gets or sets the total validated watch minutes this season.
        /// </summary>
        public long TotalWatchMinutes { get; set; }

        /// <summary>
        /// Gets or sets the number of movies completed (>=90% watched) this season.
        /// </summary>
        public int MoviesCompleted { get; set; }

        /// <summary>
        /// Gets or sets the number of series completed this season.
        /// </summary>
        public int SeriesCompleted { get; set; }

        /// <summary>
        /// Gets or sets the number of unique genres watched this season.
        /// </summary>
        public int UniqueGenresWatched { get; set; }

        /// <summary>
        /// Gets or sets the current consecutive daily watch streak.
        /// </summary>
        public int CurrentStreakDays { get; set; }

        /// <summary>
        /// Gets or sets the best consecutive daily watch streak reached this season.
        /// </summary>
        public int BestStreakDays { get; set; }

        /// <summary>
        /// Gets or sets the number of achievements unlocked this season.
        /// </summary>
        public int AchievementsUnlocked { get; set; }

        /// <summary>
        /// Gets or sets the number of approved content requests this season.
        /// </summary>
        public int ApprovedRequests { get; set; }

        /// <summary>
        /// Gets or sets the last time the user was active (watched content).
        /// </summary>
        public DateTime LastActiveUtc { get; set; }

        /// <summary>
        /// Gets or sets the last time this row was updated.
        /// </summary>
        public DateTime LastUpdatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the associated user.
        /// </summary>
        public virtual User? User { get; set; }
    }
}
