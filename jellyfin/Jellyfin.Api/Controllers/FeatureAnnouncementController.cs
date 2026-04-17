using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Models.FeatureAnnouncementDtos;
using Jellyfin.Extensions;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.FeatureAnnouncements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Feature announcement API controller.
/// </summary>
[Route("Announcement")]
public class FeatureAnnouncementController : BaseJellyfinApiController
{
    private readonly IFeatureAnnouncementService _featureAnnouncementService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureAnnouncementController"/> class.
    /// </summary>
    /// <param name="featureAnnouncementService">Feature announcement service.</param>
    public FeatureAnnouncementController(IFeatureAnnouncementService featureAnnouncementService)
    {
        _featureAnnouncementService = featureAnnouncementService;
    }

    /// <summary>
    /// Gets active published announcement campaigns for signed-in users.
    /// </summary>
    /// <returns>Announcement list.</returns>
    [HttpGet("Active")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<FeatureAnnouncementDto[]>> GetActiveAnnouncements()
    {
        var announcements = await _featureAnnouncementService
            .GetActiveAnnouncements(DateTime.UtcNow)
            .ConfigureAwait(false);

        return announcements.Select(ToDto).ToArray();
    }

    /// <summary>
    /// Gets announcement campaigns for admin management.
    /// </summary>
    /// <returns>Announcement list.</returns>
    [HttpGet("Admin")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<FeatureAnnouncementDto[]>> GetAdminAnnouncements()
    {
        var announcements = await _featureAnnouncementService
            .GetAdminAnnouncements()
            .ConfigureAwait(false);

        return announcements.Select(ToDto).ToArray();
    }

    /// <summary>
    /// Creates or updates an announcement campaign.
    /// </summary>
    /// <param name="request">Upsert request payload.</param>
    /// <returns>Updated announcement.</returns>
    [HttpPost("Admin/Upsert")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeatureAnnouncementDto>> UpsertAnnouncement([FromBody] UpsertFeatureAnnouncementRequest request)
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
            var updated = await _featureAnnouncementService
                .UpsertAnnouncement(ToUpsertInfo(request), actorUserId)
                .ConfigureAwait(false);

            return ToDto(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static FeatureAnnouncementUpsertInfo ToUpsertInfo(UpsertFeatureAnnouncementRequest request)
        => new()
        {
            Id = request.Id,
            CampaignId = request.CampaignId,
            Enabled = request.Enabled,
            Status = ParseStatus(request.Status),
            Heading = request.Heading,
            Title = request.Title,
            Subtitle = request.Subtitle,
            Description = request.Description,
            Highlights = request.Highlights ?? Array.Empty<string>(),
            HelpText = request.HelpText,
            HeroGifSource = request.HeroGifSource,
            MediaImageSource = request.MediaImageSource,
            MediaImageAlt = request.MediaImageAlt,
            MediaImageCaption = request.MediaImageCaption,
            CtaLabel = request.CtaLabel,
            CtaTargetType = ParseCtaTargetType(request.CtaTargetType),
            CtaTarget = request.CtaTarget,
            CloseLabel = request.CloseLabel,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
            MaxImpressionsPerDay = request.MaxImpressionsPerDay,
            MaxImpressionsTotal = request.MaxImpressionsTotal,
            Priority = request.Priority,
            SortOrder = request.SortOrder
        };

    private static FeatureAnnouncementStatus ParseStatus(string value)
    {
        if (Enum.TryParse(value, true, out FeatureAnnouncementStatus status))
        {
            return status;
        }

        throw new ArgumentException("Invalid announcement status.", nameof(value));
    }

    private static FeatureAnnouncementCtaTargetType ParseCtaTargetType(string value)
    {
        if (Enum.TryParse(value, true, out FeatureAnnouncementCtaTargetType targetType))
        {
            return targetType;
        }

        throw new ArgumentException("Invalid announcement CTA target type.", nameof(value));
    }

    private static FeatureAnnouncementDto ToDto(FeatureAnnouncementInfo announcement)
        => new()
        {
            Id = announcement.Id,
            CampaignId = announcement.CampaignId,
            Enabled = announcement.Enabled,
            Status = announcement.Status.ToString(),
            Heading = announcement.Heading,
            Title = announcement.Title,
            Subtitle = announcement.Subtitle,
            Description = announcement.Description,
            Highlights = announcement.Highlights,
            HelpText = announcement.HelpText,
            HeroGifSource = announcement.HeroGifSource,
            MediaImageSource = announcement.MediaImageSource,
            MediaImageAlt = announcement.MediaImageAlt,
            MediaImageCaption = announcement.MediaImageCaption,
            CtaLabel = announcement.CtaLabel,
            CtaTargetType = announcement.CtaTargetType.ToString(),
            CtaTarget = announcement.CtaTarget,
            CloseLabel = announcement.CloseLabel,
            StartsAtUtc = announcement.StartsAtUtc,
            EndsAtUtc = announcement.EndsAtUtc,
            MaxImpressionsPerDay = announcement.MaxImpressionsPerDay,
            MaxImpressionsTotal = announcement.MaxImpressionsTotal,
            Priority = announcement.Priority,
            SortOrder = announcement.SortOrder,
            CreatedAtUtc = announcement.CreatedAtUtc,
            UpdatedAtUtc = announcement.UpdatedAtUtc,
            CreatedByUserId = announcement.CreatedByUserId,
            CreatedByUsername = announcement.CreatedByUsername,
            UpdatedByUserId = announcement.UpdatedByUserId,
            UpdatedByUsername = announcement.UpdatedByUsername
        };
}
