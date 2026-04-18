using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Models.ActivityDtos;
using MediaBrowser.Controller.PersonalInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Personal insights API controller.
/// </summary>
[Route("api/activity")]
[Route("activity")]
public class PersonalInsightsController : BaseJellyfinApiController
{
    private readonly IPersonalInsightsService _personalInsightsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalInsightsController"/> class.
    /// </summary>
    /// <param name="personalInsightsService">Personal insights service.</param>
    public PersonalInsightsController(IPersonalInsightsService personalInsightsService)
    {
        _personalInsightsService = personalInsightsService;
    }

    /// <summary>
    /// Gets personal insights for the authenticated user.
    /// </summary>
    /// <param name="period">Period in {month|year|all}.</param>
    /// <param name="userId">Optional target user id. Only administrators can request another user.</param>
    /// <returns>Personal insights payload.</returns>
    [HttpGet("personal-insights")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PersonalInsightsResponseDto>> GetPersonalInsights([FromQuery] string period = "month", [FromQuery] Guid? userId = null)
    {
        var authenticatedUserId = User.GetUserId();
        if (authenticatedUserId.Equals(Guid.Empty))
        {
            return BadRequest("User is not authenticated.");
        }

        if (!TryParsePeriod(period, out var periodType))
        {
            return BadRequest("Invalid period. Use month, year, or all.");
        }

        var requestedUserId = authenticatedUserId;
        if (userId.HasValue && !userId.Value.Equals(Guid.Empty))
        {
            if (!userId.Value.Equals(authenticatedUserId) && !User.IsInRole(UserRoles.Administrator))
            {
                return Forbid();
            }

            requestedUserId = userId.Value;
        }

        var result = await _personalInsightsService.GetInsights(requestedUserId, periodType).ConfigureAwait(false);
        return ToDto(result);
    }

    private static bool TryParsePeriod(string period, out PersonalInsightsPeriodType periodType)
    {
        if (string.Equals(period, "month", StringComparison.OrdinalIgnoreCase))
        {
            periodType = PersonalInsightsPeriodType.Month;
            return true;
        }

        if (string.Equals(period, "year", StringComparison.OrdinalIgnoreCase))
        {
            periodType = PersonalInsightsPeriodType.Year;
            return true;
        }

        if (string.Equals(period, "all", StringComparison.OrdinalIgnoreCase))
        {
            periodType = PersonalInsightsPeriodType.AllTime;
            return true;
        }

        periodType = default;
        return false;
    }

    private static PersonalInsightsResponseDto ToDto(PersonalInsightsResult result)
        => new()
        {
            Summary = new PersonalInsightsSummaryDto
            {
                TotalWatchHours = result.Summary.TotalWatchHours,
                WatchTimeChangePercent = result.Summary.WatchTimeChangePercent,
                MoviesWatched = result.Summary.MoviesWatched,
                MoviesDelta = result.Summary.MoviesDelta,
                EpisodesWatched = result.Summary.EpisodesWatched,
                EpisodesDelta = result.Summary.EpisodesDelta,
                EngagementScore = result.Summary.EngagementScore,
                EngagementPercentile = result.Summary.EngagementPercentile
            },
            PeakViewing = new PersonalInsightsPeakViewingDto
            {
                HourlyDistribution = result.PeakViewing.HourlyDistribution.Select(hour => new PersonalInsightsHourlyDistributionDto
                {
                    Hour = hour.Hour,
                    Minutes = hour.Minutes
                }).ToList(),
                PeakHour = result.PeakViewing.PeakHour,
                Label = result.PeakViewing.Label,
                HasViewingActivity = result.PeakViewing.HasViewingActivity
            },
            ContinueWatching = result.ContinueWatching.Select(item => new PersonalInsightsContinueWatchingDto
            {
                ItemId = item.ItemId,
                Title = item.Title,
                SeriesName = item.SeriesName,
                SeasonNumber = item.SeasonNumber,
                EpisodeNumber = item.EpisodeNumber,
                RemainingMinutes = item.RemainingMinutes,
                ImageUrl = item.ImageUrl
            }).ToList(),
            Binge = new PersonalInsightsBingeDto
            {
                LongestStreak = result.Binge.LongestStreak,
                RecentBinges = result.Binge.RecentBinges.Select(binge => new PersonalInsightsRecentBingeDto
                {
                    SeriesName = binge.SeriesName,
                    EpisodeCount = binge.EpisodeCount
                }).ToList()
            },
            Genres = result.Genres.Select(genre => new PersonalInsightsGenreDto
            {
                Name = genre.Name,
                Minutes = genre.Minutes,
                Percentage = genre.Percentage
            }).ToList(),
            InsightText = result.InsightText
        };
}
