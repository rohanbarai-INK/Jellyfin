using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Models.LeaderboardDtos;
using Jellyfin.Extensions;
using MediaBrowser.Controller.Leaderboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Seasonal leaderboard API controller.
/// </summary>
[Route("Leaderboard")]
public class LeaderboardController : BaseJellyfinApiController
{
    private readonly ILeaderboardService _leaderboardService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeaderboardController"/> class.
    /// </summary>
    /// <param name="leaderboardService">Leaderboard service.</param>
    public LeaderboardController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    /// <summary>
    /// Gets the personal leaderboard stats for the authenticated user.
    /// </summary>
    /// <param name="seasonYear">The season year. Defaults to current year.</param>
    /// <param name="type">Metric type: xp, watchTime, movies, series, genres, streak, achievements, requests.</param>
    /// <returns>Personal leaderboard stats.</returns>
    [HttpGet("Me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeaderboardPersonalResponse>> GetPersonalStats([FromQuery] int? seasonYear = null, [FromQuery] string type = "xp")
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        var year = seasonYear ?? DateTime.UtcNow.Year;
        if (year < 2020 || year > 2100)
        {
            return BadRequest("Season year must be between 2020 and 2100.");
        }

        var info = await _leaderboardService.GetPersonalStats(userId, year, type).ConfigureAwait(false);
        return new LeaderboardPersonalResponse
        {
            SeasonYear = info.SeasonYear,
            TotalXp = info.TotalXp,
            AchievementXp = info.AchievementXp,
            AchievementCount = info.AchievementCount,
            Level = info.Level,
            Rank = info.Rank,
            Percentile = info.Percentile,
            TotalUsers = info.TotalUsers,
            RankTitle = info.RankTitle,
            RankEmoji = info.RankEmoji,
            TotalWatchMinutes = info.TotalWatchMinutes,
            MoviesCompleted = info.MoviesCompleted,
            SeriesCompleted = info.SeriesCompleted,
            UniqueGenresWatched = info.UniqueGenresWatched,
            CurrentStreakDays = info.CurrentStreakDays,
            AchievementsUnlocked = info.AchievementsUnlocked,
            ApprovedRequests = info.ApprovedRequests,
            MetricValue = info.MetricValue,
            MetricType = info.MetricType,
            MetricLabel = info.MetricLabel,
            BestStreakDays = info.BestStreakDays,
            GapToNext = info.GapToNext,
            GapToTop = info.GapToTop,
            CurrentUserEntry = info.CurrentUserEntry is not null ? ToDto(info.CurrentUserEntry) : null,
            NextTarget = info.NextTarget is not null ? ToCompetitionDto(info.NextTarget) : null,
            BehindUser = info.BehindUser is not null ? ToCompetitionDto(info.BehindUser) : null
        };
    }

    /// <summary>
    /// Gets the top leaderboard entries for a season.
    /// </summary>
    /// <param name="seasonYear">The season year. Defaults to current year.</param>
    /// <param name="offset">Number of rows to skip.</param>
    /// <param name="limit">Maximum entries to return (default 50, max 100).</param>
    /// <param name="type">Metric type: xp, watchTime, movies, series, genres, streak, achievements, requests.</param>
    /// <returns>Top leaderboard.</returns>
    [HttpGet("Top")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeaderboardTopResponse>> GetTopLeaderboard([FromQuery] int? seasonYear = null, [FromQuery] int offset = 0, [FromQuery] int limit = 10, [FromQuery] string type = "xp")
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        var year = seasonYear ?? DateTime.UtcNow.Year;
        if (year < 2020 || year > 2100)
        {
            return BadRequest("Season year must be between 2020 and 2100.");
        }

        if (offset < 0)
        {
            return BadRequest("Offset must be 0 or greater.");
        }

        var result = await _leaderboardService.GetTopLeaderboard(userId, year, offset, limit, type).ConfigureAwait(false);
        return new LeaderboardTopResponse
        {
            SeasonYear = result.SeasonYear,
            MetricType = result.MetricType,
            Entries = result.Entries.Select(ToDto).ToList(),
            CurrentUserPosition = result.CurrentUserPosition is not null ? ToDto(result.CurrentUserPosition) : null,
            TotalUsers = result.TotalUsers,
            Offset = result.Offset,
            Limit = result.Limit,
            HasMore = result.HasMore
        };
    }

    private static LeaderboardEntryDto ToDto(LeaderboardEntryInfo entry) => new()
    {
        UserId = entry.UserId,
        UserName = entry.UserName,
        PrimaryImageTag = entry.PrimaryImageTag,
        Rank = entry.Rank,
        TotalXp = entry.TotalXp,
        Level = entry.Level,
        AchievementCount = entry.AchievementCount,
        RankTitle = entry.RankTitle,
        RankEmoji = entry.RankEmoji,
        BadgeKind = entry.BadgeKind,
        IsCurrentUser = entry.IsCurrentUser,
        MetricValue = entry.MetricValue,
        MetricLabel = entry.MetricLabel,
        SecondaryMetricValue = entry.SecondaryMetricValue,
        SecondaryMetricLabel = entry.SecondaryMetricLabel
    };

    private static LeaderboardCompetitionDto ToCompetitionDto(LeaderboardCompetitionInfo competition) => new()
    {
        Entry = ToDto(competition.Entry),
        GapValue = competition.GapValue,
        GapLabel = competition.GapLabel
    };
}
