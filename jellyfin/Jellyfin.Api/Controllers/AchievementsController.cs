using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Models.AchievementDtos;
using Jellyfin.Extensions;
using MediaBrowser.Controller.Achievements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Achievements API controller.
/// </summary>
[Route("Achievements")]
public class AchievementsController : BaseJellyfinApiController
{
    private readonly IAchievementService _achievementService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AchievementsController"/> class.
    /// </summary>
    /// <param name="achievementService">Achievement service.</param>
    public AchievementsController(IAchievementService achievementService)
    {
        _achievementService = achievementService;
    }

    /// <summary>
    /// Gets achievement definitions.
    /// </summary>
    /// <param name="includeSeasonal">A value indicating whether seasonal rows should be included.</param>
    /// <returns>Achievement definitions.</returns>
    [HttpGet("Definitions")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AchievementDefinitionDto>>> GetDefinitions([FromQuery] bool includeSeasonal = false)
    {
        var rows = await _achievementService.GetDefinitions(includeSeasonal).ConfigureAwait(false);
        return rows.Select(ToDto).ToList();
    }

    /// <summary>
    /// Gets achievement history for the authenticated user.
    /// </summary>
    /// <param name="userId">Optional user id. Non-admin users can only request their own history.</param>
    /// <param name="take">Maximum rows to return.</param>
    /// <returns>Achievement history rows.</returns>
    [HttpGet("History")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UserAchievementDto>>> GetHistory([FromQuery] Guid? userId = null, [FromQuery] int take = 200)
    {
        var authenticatedUserId = User.GetUserId();
        if (authenticatedUserId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        var requestedUserId = authenticatedUserId;
        if (userId.HasValue && !userId.Value.IsEmpty())
        {
            if (!userId.Value.Equals(authenticatedUserId) && !User.IsInRole(UserRoles.Administrator))
            {
                return Forbid();
            }

            requestedUserId = userId.Value;
        }

        try
        {
            var rows = await _achievementService.GetHistory(requestedUserId, take).ConfigureAwait(false);
            return rows.Select(ToDto).ToList();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Unlocks an achievement for the authenticated user.
    /// </summary>
    /// <param name="request">Unlock request payload.</param>
    /// <returns>Unlock result.</returns>
    [HttpPost("Unlock")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AchievementUnlockResponse>> Unlock([FromBody, Required] UnlockAchievementRequest request)
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        try
        {
            var result = await _achievementService.Unlock(userId, request.AchievementId).ConfigureAwait(false);
            return ToDto(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (AchievementNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Evaluates all milestone rules for the authenticated user and unlocks newly met achievements.
    /// </summary>
    /// <returns>Newly unlocked achievements in this sync run.</returns>
    [HttpPost("Sync")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AchievementSyncResponse>> Sync()
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        try
        {
            var rows = await _achievementService.Sync(userId).ConfigureAwait(false);
            return new AchievementSyncResponse
            {
                UnlockedAchievements = rows.Select(ToDto).ToList()
            };
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static AchievementDefinitionDto ToDto(AchievementDefinitionInfo row)
        => new()
        {
            Id = row.Id,
            Title = row.Title,
            Description = row.Description,
            ImageEmoji = row.ImageEmoji,
            Rarity = row.Rarity,
            Xp = row.Xp,
            Coins = row.Coins,
            IsSeasonal = row.IsSeasonal,
            SeasonType = row.SeasonType
        };

    private static UserAchievementDto ToDto(UserAchievementInfo row)
        => new()
        {
            Id = row.Id,
            Title = row.Title,
            Description = row.Description,
            ImageEmoji = row.ImageEmoji,
            Rarity = row.Rarity,
            Xp = row.Xp,
            Coins = row.Coins,
            UnlockedAt = row.UnlockedAt,
            IsSeasonal = row.IsSeasonal,
            SeasonType = row.SeasonType,
            SeasonYear = row.SeasonYear
        };

    private static AchievementUnlockResponse ToDto(AchievementUnlockResult row)
        => new()
        {
            Unlocked = row.Unlocked,
            Achievement = ToDto(row.Achievement)
        };
}
