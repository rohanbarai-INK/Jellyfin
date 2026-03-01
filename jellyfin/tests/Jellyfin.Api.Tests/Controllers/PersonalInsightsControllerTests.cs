using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Controllers;
using Jellyfin.Api.Models.ActivityDtos;
using MediaBrowser.Controller.PersonalInsights;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Controllers;

public class PersonalInsightsControllerTests
{
    private readonly Mock<IPersonalInsightsService> _mockPersonalInsightsService;
    private readonly PersonalInsightsController _subject;

    public PersonalInsightsControllerTests()
    {
        _mockPersonalInsightsService = new Mock<IPersonalInsightsService>(MockBehavior.Strict);
        _subject = new PersonalInsightsController(_mockPersonalInsightsService.Object);
    }

    [Fact]
    public async Task GetPersonalInsights_WhenInvalidPeriod_ReturnsBadRequest()
    {
        SetUser(Guid.NewGuid(), isAdministrator: false);

        var result = await _subject.GetPersonalInsights("weekly", null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(result.Value);
        _mockPersonalInsightsService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPersonalInsights_WhenRequestingOwnInsights_CallsServiceWithAuthenticatedUser()
    {
        var authenticatedUserId = Guid.NewGuid();
        SetUser(authenticatedUserId, isAdministrator: false);
        var payload = CreateResult();
        _mockPersonalInsightsService
            .Setup(service => service.GetInsights(authenticatedUserId, PersonalInsightsPeriodType.Month))
            .ReturnsAsync(payload);

        var result = await _subject.GetPersonalInsights("month", null);

        var dto = Assert.IsType<PersonalInsightsResponseDto>(result.Value);
        Assert.Equal(payload.Summary.TotalWatchHours, dto.Summary.TotalWatchHours);
        _mockPersonalInsightsService.Verify(service => service.GetInsights(authenticatedUserId, PersonalInsightsPeriodType.Month), Times.Once);
    }

    [Fact]
    public async Task GetPersonalInsights_WhenNonAdminRequestsAnotherUser_ReturnsForbid()
    {
        var authenticatedUserId = Guid.NewGuid();
        SetUser(authenticatedUserId, isAdministrator: false);

        var result = await _subject.GetPersonalInsights("month", Guid.NewGuid());

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Null(result.Value);
        _mockPersonalInsightsService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPersonalInsights_WhenAdminRequestsAnotherUser_CallsServiceWithRequestedUser()
    {
        var authenticatedUserId = Guid.NewGuid();
        var requestedUserId = Guid.NewGuid();
        SetUser(authenticatedUserId, isAdministrator: true);
        var payload = CreateResult();
        _mockPersonalInsightsService
            .Setup(service => service.GetInsights(requestedUserId, PersonalInsightsPeriodType.Year))
            .ReturnsAsync(payload);

        var result = await _subject.GetPersonalInsights("year", requestedUserId);

        Assert.IsType<PersonalInsightsResponseDto>(result.Value);
        _mockPersonalInsightsService.Verify(service => service.GetInsights(requestedUserId, PersonalInsightsPeriodType.Year), Times.Once);
    }

    private void SetUser(Guid userId, bool isAdministrator)
    {
        var role = isAdministrator ? UserRoles.Administrator : UserRoles.User;
        var claims = new[]
        {
            new Claim(InternalClaimTypes.UserId, userId.ToString("N")),
            new Claim(ClaimTypes.Role, role)
        };

        _subject.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    private static PersonalInsightsResult CreateResult()
        => new()
        {
            Summary = new PersonalInsightsSummaryResult
            {
                TotalWatchHours = 13,
                WatchTimeChangePercent = 12,
                MoviesWatched = 3,
                MoviesDelta = 2,
                EpisodesWatched = 6,
                EpisodesDelta = 1,
                EngagementScore = "High",
                EngagementPercentile = 95
            },
            PeakViewing = new PersonalInsightsPeakViewingResult
            {
                HourlyDistribution =
                [
                    new PersonalInsightsHourlyDistributionResult
                    {
                        Hour = 20,
                        Minutes = 40
                    }
                ],
                PeakHour = 20,
                Label = "Night Owl"
            },
            ContinueWatching = [],
            Binge = new PersonalInsightsBingeResult
            {
                LongestStreak = 0,
                RecentBinges = []
            },
            Genres = [],
            InsightText = "You've spent 22% of your time watching Sci-Fi this month."
        };
}
