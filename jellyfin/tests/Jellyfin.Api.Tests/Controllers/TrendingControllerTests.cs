using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Controllers;
using Jellyfin.Api.Models.ActivityDtos;
using MediaBrowser.Controller.Trending;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Controllers;

public class TrendingControllerTests
{
    private readonly Mock<ITrendingNowService> _mockTrendingNowService;
    private readonly Mock<ITrendingPromotionService> _mockTrendingPromotionService;
    private readonly TrendingController _subject;

    public TrendingControllerTests()
    {
        _mockTrendingNowService = new Mock<ITrendingNowService>(MockBehavior.Strict);
        _mockTrendingPromotionService = new Mock<ITrendingPromotionService>(MockBehavior.Strict);
        _subject = new TrendingController(_mockTrendingNowService.Object, _mockTrendingPromotionService.Object);
    }

    [Fact]
    public async Task GetTrendingNow_WhenInvalidPeriod_ReturnsBadRequest()
    {
        SetUser(Guid.NewGuid(), true);

        var result = await _subject.GetTrendingNow("daily", 16);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(result.Value);
        _mockTrendingNowService.VerifyNoOtherCalls();
        _mockTrendingPromotionService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTrendingNow_WhenLimitOutOfRange_ReturnsBadRequest()
    {
        SetUser(Guid.NewGuid(), true);

        var result = await _subject.GetTrendingNow("week", 0);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(result.Value);
        _mockTrendingNowService.VerifyNoOtherCalls();
        _mockTrendingPromotionService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTrendingNow_WhenValidRequest_ReturnsPayload()
    {
        var userId = Guid.NewGuid();
        SetUser(userId, true);
        var itemId = Guid.NewGuid();
        var payload = new TrendingNowResult
        {
            PeriodKey = "W:20260420",
            PeriodLabel = "This Week",
            PeriodStartUtc = new DateTime(2026, 4, 19, 18, 30, 0, DateTimeKind.Utc),
            PeriodEndUtc = new DateTime(2026, 4, 26, 18, 30, 0, DateTimeKind.Utc),
            Limit = 16,
            CandidateCount = 22,
            UsedFallbackMode = false,
            Items =
            [
                new TrendingNowItemResult
                {
                    ItemId = itemId,
                    ItemType = "Series",
                    Title = "The Example Show",
                    Rank = 1,
                    BaseScore = 91.4,
                    PersonalizationBoost = 8.5,
                    AdminBoost = 12,
                    FinalScore = 111.9,
                    TotalWatchHours = 14.5,
                    UniqueViewers = 8,
                    Starts = 11,
                    Completions = 4,
                    MomentumWatchHours = 6.2,
                    PromotionId = Guid.NewGuid(),
                    PinPosition = 1,
                    IsAdminPromoted = true,
                    PrimaryLabel = "Featured",
                    SecondaryLabel = "#1 This Week",
                    ExplanationText = "Featured by KnightFlix",
                    ExplanationSource = TrendingExplanationSource.AdminPromotion,
                    Tagline = "Watched by 8 users this week",
                    MatchedGenre = "Action",
                    AudienceSegment = TrendingAudienceSegment.MovieHeavy,
                    Overview = "Overview",
                    Genres = new List<string> { "Action", "Thriller" },
                    ProductionYear = 2024,
                    RunTimeTicks = TimeSpan.FromMinutes(120).Ticks,
                    OfficialRating = "PG-13",
                    HasPrimaryImage = true,
                    HasBackdropImage = true,
                    ContextText = "Watched by 8 users this week"
                }
            ]
        };
        _mockTrendingNowService
            .Setup(service => service.GetTrendingNow(userId, TrendingNowPeriodType.Week, 16))
            .ReturnsAsync(payload);

        var result = await _subject.GetTrendingNow("week", 16);

        var dto = Assert.IsType<TrendingNowResponseDto>(result.Value);
        Assert.Equal(payload.PeriodKey, dto.PeriodKey);
        Assert.Equal(payload.CandidateCount, dto.CandidateCount);
        Assert.False(dto.UsedFallbackMode);
        Assert.Single(dto.Items);
        Assert.Equal(payload.Items[0].PrimaryLabel, dto.Items[0].PrimaryLabel);
        Assert.Equal(payload.Items[0].FinalScore, dto.Items[0].FinalScore);
        Assert.Equal(payload.Items[0].ExplanationSource, dto.Items[0].ExplanationSource);
        _mockTrendingNowService.Verify(service => service.GetTrendingNow(userId, TrendingNowPeriodType.Week, 16), Times.Once);
        _mockTrendingPromotionService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPromotions_ReturnsPromotionList()
    {
        SetUser(Guid.NewGuid(), true);
        var promotions = new[]
        {
            new TrendingPromotionInfo
            {
                Id = Guid.NewGuid(),
                PromotionId = "hero-campaign",
                ItemId = Guid.NewGuid(),
                ItemTitle = "Hero Title",
                Enabled = true,
                PinPosition = 1,
                BoostAmount = 25,
                AudienceSegment = TrendingAudienceSegment.AllUsers,
                LabelOverride = "Featured",
                TaglineOverride = "Top pick",
                ArtworkVariant = "backdrop",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                CreatedByUsername = "admin",
                UpdatedByUsername = "admin"
            }
        };

        _mockTrendingPromotionService
            .Setup(service => service.GetAdminPromotions())
            .ReturnsAsync(promotions);

        var result = await _subject.GetPromotions();

        var dto = Assert.IsType<TrendingPromotionDto[]>(result.Value);
        Assert.Single(dto);
        Assert.Equal(promotions[0].PromotionId, dto[0].PromotionId);
        Assert.Equal(promotions[0].AudienceSegment.ToString(), dto[0].AudienceSegment);
        _mockTrendingPromotionService.Verify(service => service.GetAdminPromotions(), Times.Once);
        _mockTrendingNowService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpsertPromotion_WhenRequestMissing_ReturnsBadRequest()
    {
        SetUser(Guid.NewGuid(), true);

        var result = await _subject.UpsertPromotion(null!);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _mockTrendingNowService.VerifyNoOtherCalls();
        _mockTrendingPromotionService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpsertPromotion_WhenValidRequest_ReturnsUpdatedPromotion()
    {
        var userId = Guid.NewGuid();
        SetUser(userId, true);
        var request = new UpsertTrendingPromotionRequest
        {
            PromotionId = "featured-action",
            ItemId = Guid.NewGuid(),
            Enabled = true,
            PinPosition = 1,
            BoostAmount = 20,
            AudienceSegment = TrendingAudienceSegment.TopGenreMatch.ToString(),
            AudienceValue = "Action",
            LabelOverride = "Featured",
            TaglineOverride = "Action is surging"
        };

        _mockTrendingPromotionService
            .Setup(service => service.UpsertPromotion(
                It.Is<TrendingPromotionUpsertInfo>(info =>
                    info.PromotionId == request.PromotionId
                    && info.ItemId.Equals(request.ItemId)
                    && info.Enabled == request.Enabled
                    && info.PinPosition == request.PinPosition
                    && info.BoostAmount == request.BoostAmount
                    && info.AudienceSegment == TrendingAudienceSegment.TopGenreMatch
                    && info.AudienceValue == request.AudienceValue
                    && info.LabelOverride == request.LabelOverride
                    && info.TaglineOverride == request.TaglineOverride),
                userId))
            .ReturnsAsync(new TrendingPromotionInfo
            {
                Id = Guid.NewGuid(),
                PromotionId = request.PromotionId,
                ItemId = request.ItemId,
                ItemTitle = "Action Title",
                Enabled = request.Enabled,
                PinPosition = request.PinPosition,
                BoostAmount = request.BoostAmount,
                AudienceSegment = TrendingAudienceSegment.TopGenreMatch,
                AudienceValue = request.AudienceValue,
                LabelOverride = request.LabelOverride,
                TaglineOverride = request.TaglineOverride,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                CreatedByUsername = "admin",
                UpdatedByUsername = "admin"
            });

        var result = await _subject.UpsertPromotion(request);

        var dto = Assert.IsType<TrendingPromotionDto>(result.Value);
        Assert.Equal(request.PromotionId, dto.PromotionId);
        Assert.Equal(request.ItemId, dto.ItemId);
        Assert.Equal(request.AudienceSegment, dto.AudienceSegment);
        _mockTrendingPromotionService.VerifyAll();
        _mockTrendingNowService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SetPromotionEnabled_WhenValidRequest_ReturnsUpdatedPromotion()
    {
        var userId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();
        SetUser(userId, true);

        _mockTrendingPromotionService
            .Setup(service => service.SetEnabled(promotionId, false, userId))
            .ReturnsAsync(new TrendingPromotionInfo
            {
                Id = promotionId,
                PromotionId = "test-promo",
                ItemId = Guid.NewGuid(),
                ItemTitle = "Hero Title",
                Enabled = false,
                AudienceSegment = TrendingAudienceSegment.AllUsers,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                CreatedByUsername = "admin",
                UpdatedByUsername = "admin"
            });

        var result = await _subject.SetPromotionEnabled(promotionId, new SetTrendingPromotionEnabledRequest { Enabled = false });

        var dto = Assert.IsType<TrendingPromotionDto>(result.Value);
        Assert.False(dto.Enabled);
        _mockTrendingPromotionService.Verify(service => service.SetEnabled(promotionId, false, userId), Times.Once);
        _mockTrendingNowService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeletePromotion_WhenValidRequest_ReturnsNoContent()
    {
        var promotionId = Guid.NewGuid();
        SetUser(Guid.NewGuid(), true);

        _mockTrendingPromotionService
            .Setup(service => service.DeletePromotion(promotionId))
            .Returns(Task.CompletedTask);

        var result = await _subject.DeletePromotion(promotionId);

        Assert.IsType<NoContentResult>(result);
        _mockTrendingPromotionService.Verify(service => service.DeletePromotion(promotionId), Times.Once);
        _mockTrendingNowService.VerifyNoOtherCalls();
    }

    private void SetUser(Guid userId, bool isAdmin)
    {
        var claims = new List<Claim>
        {
            new(InternalClaimTypes.UserId, userId.ToString("N")),
            new(ClaimTypes.Role, UserRoles.User)
        };

        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, UserRoles.Administrator));
        }

        _subject.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }
}
