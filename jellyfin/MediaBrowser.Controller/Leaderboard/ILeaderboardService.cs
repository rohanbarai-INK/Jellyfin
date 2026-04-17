using System;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Leaderboard
{
    /// <summary>
    /// Handles seasonal leaderboard stats, ranking and retrieval.
    /// </summary>
    public interface ILeaderboardService
    {
        /// <summary>
        /// Gets the personal leaderboard stats for a user in a season.
        /// If no cached stats exist, computes them from achievement history.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="seasonYear">The season year.</param>
        /// <param name="metricType">The metric type to rank by (default: xp).</param>
        /// <returns>Personal leaderboard info.</returns>
        Task<LeaderboardPersonalInfo> GetPersonalStats(Guid userId, int seasonYear, string metricType = "xp");

        /// <summary>
        /// Gets the top leaderboard entries for a season.
        /// </summary>
        /// <param name="requestingUserId">The requesting user's id (for currentUser flag).</param>
        /// <param name="seasonYear">The season year.</param>
        /// <param name="offset">Number of rows to skip.</param>
        /// <param name="limit">Maximum entries to return.</param>
        /// <param name="metricType">The metric type to rank by (default: xp).</param>
        /// <returns>Top leaderboard result.</returns>
        Task<LeaderboardTopResult> GetTopLeaderboard(Guid requestingUserId, int seasonYear, int offset, int limit, string metricType = "xp");

        /// <summary>
        /// Adds achievement XP to a user's seasonal stats when an achievement is unlocked.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="seasonYear">The season year of the unlock.</param>
        /// <param name="xp">The XP to add.</param>
        /// <param name="coins">The coins earned (tracked for reference).</param>
        /// <returns>A task.</returns>
        Task RecordAchievementXp(Guid userId, int seasonYear, int xp, int coins);

        /// <summary>
        /// Records playback stats after a watch session completes.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="seasonYear">The season year.</param>
        /// <param name="validatedMinutes">Validated watch minutes from the session.</param>
        /// <param name="movieCompleted">Whether a movie was completed.</param>
        /// <param name="episodeCompleted">Whether an episode was completed.</param>
        /// <param name="genres">Genres of the watched item.</param>
        /// <returns>A task.</returns>
        Task RecordPlaybackStats(Guid userId, int seasonYear, long validatedMinutes, bool movieCompleted, bool episodeCompleted, string[] genres);

        /// <summary>
        /// Increments approved request count for the user's season.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="seasonYear">The season year.</param>
        /// <returns>A task.</returns>
        Task RecordApprovedRequest(Guid userId, int seasonYear);
    }
}
