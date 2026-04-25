using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Models.ActivityDtos;
using Jellyfin.Extensions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Trending;
using MediaBrowser.Model.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Trending Now API controller.
/// </summary>
[Route("api/activity")]
[Route("activity")]
public class TrendingController : BaseJellyfinApiController
{
    private const string TrendingNowConfigKey = "trendingnow";
    private const int MinMaxSlides = 1;
    private const int MaxMaxSlides = 30;

    private readonly ITrendingNowService _trendingNowService;
    private readonly ITrendingPromotionService _trendingPromotionService;
    private readonly IConfigurationManager _configurationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrendingController"/> class.
    /// </summary>
    /// <param name="trendingNowService">Trending service.</param>
    /// <param name="trendingPromotionService">Promotion service.</param>
    /// <param name="configurationManager">Server configuration manager.</param>
    public TrendingController(
        ITrendingNowService trendingNowService,
        ITrendingPromotionService trendingPromotionService,
        IConfigurationManager configurationManager)
    {
        _trendingNowService = trendingNowService;
        _trendingPromotionService = trendingPromotionService;
        _configurationManager = configurationManager;
    }

    /// <summary>
    /// Gets the current Trending Now rail.
    /// </summary>
    /// <param name="period">Period in {week|month|season}.</param>
    /// <param name="limit">Maximum rows to return.</param>
    /// <returns>Trending discovery rows.</returns>
    [HttpGet("trending-now")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TrendingNowResponseDto>> GetTrendingNow([FromQuery] string period = "week", [FromQuery] int limit = 16)
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        if (!TryParsePeriod(period, out var periodType))
        {
            return BadRequest("Invalid period. Use week, month, or season.");
        }

        if (limit <= 0 || limit > 30)
        {
            return BadRequest("Limit must be between 1 and 30.");
        }

        var result = await _trendingNowService.GetTrendingNow(userId, periodType, limit).ConfigureAwait(false);
        return new TrendingNowResponseDto
        {
            PeriodKey = result.PeriodKey,
            PeriodLabel = result.PeriodLabel,
            PeriodStartUtc = result.PeriodStartUtc,
            PeriodEndUtc = result.PeriodEndUtc,
            Limit = result.Limit,
            CandidateCount = result.CandidateCount,
            UsedFallbackMode = result.UsedFallbackMode,
            Items = result.Items.Select(ToTrendingItemDto).ToList()
        };
    }

    /// <summary>
    /// Gets homepage Trending rail settings.
    /// </summary>
    /// <returns>Trending settings.</returns>
    [HttpGet("trending-now/settings")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<TrendingNowSettingsDto> GetTrendingNowSettings()
    {
        var options = GetTrendingNowOptions();
        return new TrendingNowSettingsDto
        {
            MaxSlides = options.MaxSlides
        };
    }

    /// <summary>
    /// Updates homepage Trending rail settings.
    /// </summary>
    /// <param name="request">Settings payload.</param>
    /// <returns>Updated settings.</returns>
    [HttpPost("trending-now/settings")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<TrendingNowSettingsDto> SetTrendingNowSettings([FromBody] SetTrendingNowSettingsRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request payload is required.");
        }

        if (request.MaxSlides < MinMaxSlides || request.MaxSlides > MaxMaxSlides)
        {
            return BadRequest($"MaxSlides must be between {MinMaxSlides} and {MaxMaxSlides}.");
        }

        var options = GetTrendingNowOptions();
        options.MaxSlides = request.MaxSlides;
        _configurationManager.SaveConfiguration(TrendingNowConfigKey, options);

        return new TrendingNowSettingsDto
        {
            MaxSlides = options.MaxSlides
        };
    }

    /// <summary>
    /// Gets admin-configured Trending promotions.
    /// </summary>
    /// <returns>Promotion rows.</returns>
    [HttpGet("trending-now/promotions")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<TrendingPromotionDto[]>> GetPromotions()
    {
        var promotions = await _trendingPromotionService.GetAdminPromotions().ConfigureAwait(false);
        return promotions.Select(ToPromotionDto).ToArray();
    }

    /// <summary>
    /// Creates or updates a Trending promotion.
    /// </summary>
    /// <param name="request">Upsert request payload.</param>
    /// <returns>Updated promotion row.</returns>
    [HttpPost("trending-now/promotions/upsert")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TrendingPromotionDto>> UpsertPromotion([FromBody] UpsertTrendingPromotionRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request payload is required.");
        }

        var actorUserId = User.GetUserId();
        if (actorUserId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        try
        {
            var updated = await _trendingPromotionService
                .UpsertPromotion(ToPromotionUpsertInfo(request), actorUserId)
                .ConfigureAwait(false);

            return ToPromotionDto(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Enables or disables a Trending promotion.
    /// </summary>
    /// <param name="promotionId">Promotion id.</param>
    /// <param name="request">Enabled payload.</param>
    /// <returns>Updated promotion row.</returns>
    [HttpPost("trending-now/promotions/{promotionId:guid}/enabled")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TrendingPromotionDto>> SetPromotionEnabled([FromRoute] Guid promotionId, [FromBody] SetTrendingPromotionEnabledRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request payload is required.");
        }

        var actorUserId = User.GetUserId();
        if (actorUserId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        try
        {
            var updated = await _trendingPromotionService
                .SetEnabled(promotionId, request.Enabled, actorUserId)
                .ConfigureAwait(false);

            return ToPromotionDto(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Deletes a Trending promotion.
    /// </summary>
    /// <param name="promotionId">Promotion id.</param>
    /// <returns>No content.</returns>
    [HttpDelete("trending-now/promotions/{promotionId:guid}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeletePromotion([FromRoute] Guid promotionId)
    {
        try
        {
            await _trendingPromotionService.DeletePromotion(promotionId).ConfigureAwait(false);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static TrendingNowItemDto ToTrendingItemDto(TrendingNowItemResult item)
        => new()
        {
            ItemId = item.ItemId,
            ItemType = item.ItemType,
            Title = item.Title,
            Rank = item.Rank,
            BaseScore = item.BaseScore,
            PersonalizationBoost = item.PersonalizationBoost,
            AdminBoost = item.AdminBoost,
            FinalScore = item.FinalScore,
            TotalWatchHours = item.TotalWatchHours,
            UniqueViewers = item.UniqueViewers,
            Starts = item.Starts,
            Completions = item.Completions,
            MomentumWatchHours = item.MomentumWatchHours,
            PromotionId = item.PromotionId,
            PinPosition = item.PinPosition,
            IsAdminPromoted = item.IsAdminPromoted,
            PrimaryLabel = item.PrimaryLabel,
            SecondaryLabel = item.SecondaryLabel,
            ExplanationText = item.ExplanationText,
            ExplanationSource = item.ExplanationSource,
            Tagline = item.Tagline,
            MatchedGenre = item.MatchedGenre,
            AudienceSegment = item.AudienceSegment,
            Overview = item.Overview,
            Genres = item.Genres,
            ProductionYear = item.ProductionYear,
            RunTimeTicks = item.RunTimeTicks,
            OfficialRating = item.OfficialRating,
            HasPrimaryImage = item.HasPrimaryImage,
            HasBackdropImage = item.HasBackdropImage,
            ContextText = item.ContextText
        };

    private static TrendingPromotionDto ToPromotionDto(TrendingPromotionInfo promotion)
        => new()
        {
            Id = promotion.Id,
            PromotionId = promotion.PromotionId,
            ItemId = promotion.ItemId,
            ItemTitle = promotion.ItemTitle,
            Enabled = promotion.Enabled,
            StartsAtUtc = promotion.StartsAtUtc,
            EndsAtUtc = promotion.EndsAtUtc,
            PinPosition = promotion.PinPosition,
            BoostAmount = promotion.BoostAmount,
            AudienceSegment = promotion.AudienceSegment.ToString(),
            AudienceValue = promotion.AudienceValue,
            LabelOverride = promotion.LabelOverride,
            TaglineOverride = promotion.TaglineOverride,
            ArtworkVariant = promotion.ArtworkVariant,
            CreatedAtUtc = promotion.CreatedAtUtc,
            UpdatedAtUtc = promotion.UpdatedAtUtc,
            CreatedByUsername = promotion.CreatedByUsername,
            UpdatedByUsername = promotion.UpdatedByUsername
        };

    private static TrendingPromotionUpsertInfo ToPromotionUpsertInfo(UpsertTrendingPromotionRequest request)
        => new()
        {
            Id = request.Id,
            PromotionId = request.PromotionId,
            ItemId = request.ItemId,
            Enabled = request.Enabled,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
            PinPosition = request.PinPosition,
            BoostAmount = request.BoostAmount,
            AudienceSegment = ParseAudienceSegment(request.AudienceSegment),
            AudienceValue = request.AudienceValue,
            LabelOverride = request.LabelOverride,
            TaglineOverride = request.TaglineOverride,
            ArtworkVariant = request.ArtworkVariant
        };

    private static TrendingAudienceSegment ParseAudienceSegment(string value)
    {
        if (Enum.TryParse(value, true, out TrendingAudienceSegment segment))
        {
            return segment;
        }

        throw new ArgumentException("Invalid Trending audience segment.", nameof(value));
    }

    private static bool TryParsePeriod(string period, out TrendingNowPeriodType periodType)
    {
        if (string.Equals(period, "week", StringComparison.OrdinalIgnoreCase))
        {
            periodType = TrendingNowPeriodType.Week;
            return true;
        }

        if (string.Equals(period, "month", StringComparison.OrdinalIgnoreCase))
        {
            periodType = TrendingNowPeriodType.Month;
            return true;
        }

        if (string.Equals(period, "season", StringComparison.OrdinalIgnoreCase))
        {
            periodType = TrendingNowPeriodType.Season;
            return true;
        }

        periodType = default;
        return false;
    }

    private TrendingNowOptions GetTrendingNowOptions()
    {
        var options = _configurationManager.GetConfiguration<TrendingNowOptions>(TrendingNowConfigKey);
        var normalized = options ?? new TrendingNowOptions();
        normalized.MaxSlides = Math.Clamp(normalized.MaxSlides, MinMaxSlides, MaxMaxSlides);
        return normalized;
    }
}
