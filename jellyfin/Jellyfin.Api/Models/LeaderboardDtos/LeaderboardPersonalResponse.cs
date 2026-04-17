namespace Jellyfin.Api.Models.LeaderboardDtos;

/// <summary>
/// Personal leaderboard response.
/// </summary>
public class LeaderboardPersonalResponse
{
    /// <summary>
    /// Gets or sets the season year.
    /// </summary>
    public int SeasonYear { get; set; }

    /// <summary>
    /// Gets or sets the total XP earned this season.
    /// </summary>
    public long TotalXp { get; set; }

    /// <summary>
    /// Gets or sets the achievement XP earned this season.
    /// </summary>
    public long AchievementXp { get; set; }

    /// <summary>
    /// Gets or sets the number of achievements unlocked this season.
    /// </summary>
    public int AchievementCount { get; set; }

    /// <summary>
    /// Gets or sets the computed level.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets the rank position (1-based).
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Gets or sets the percentile (0-100).
    /// </summary>
    public double Percentile { get; set; }

    /// <summary>
    /// Gets or sets the total number of users in this season.
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Gets or sets the rank title.
    /// </summary>
    public string RankTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rank emoji.
    /// </summary>
    public string RankEmoji { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets total watch minutes.
    /// </summary>
    public long TotalWatchMinutes { get; set; }

    /// <summary>
    /// Gets or sets movies completed.
    /// </summary>
    public int MoviesCompleted { get; set; }

    /// <summary>
    /// Gets or sets series completed.
    /// </summary>
    public int SeriesCompleted { get; set; }

    /// <summary>
    /// Gets or sets unique genres watched.
    /// </summary>
    public int UniqueGenresWatched { get; set; }

    /// <summary>
    /// Gets or sets current streak days.
    /// </summary>
    public int CurrentStreakDays { get; set; }

    /// <summary>
    /// Gets or sets achievements unlocked.
    /// </summary>
    public int AchievementsUnlocked { get; set; }

    /// <summary>
    /// Gets or sets approved requests.
    /// </summary>
    public int ApprovedRequests { get; set; }

    /// <summary>
    /// Gets or sets the metric value for the requested type.
    /// </summary>
    public long MetricValue { get; set; }

    /// <summary>
    /// Gets or sets the requested metric type.
    /// </summary>
    public string MetricType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the formatted label for the metric value.
    /// </summary>
    public string MetricLabel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the best streak days this season.
    /// </summary>
    public int BestStreakDays { get; set; }

    /// <summary>
    /// Gets or sets the gap to next rank.
    /// </summary>
    public long GapToNext { get; set; }

    /// <summary>
    /// Gets or sets the gap to top rank.
    /// </summary>
    public long GapToTop { get; set; }

    /// <summary>
    /// Gets or sets the current user's leaderboard row.
    /// </summary>
    public LeaderboardEntryDto? CurrentUserEntry { get; set; }

    /// <summary>
    /// Gets or sets the user directly above the current user.
    /// </summary>
    public LeaderboardCompetitionDto? NextTarget { get; set; }

    /// <summary>
    /// Gets or sets the user directly below the current user.
    /// </summary>
    public LeaderboardCompetitionDto? BehindUser { get; set; }
}
