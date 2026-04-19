using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Models.ContentRequestDtos;
using Jellyfin.Extensions;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.ContentRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Content request API controller.
/// </summary>
[Route("Request")]
public class RequestController : BaseJellyfinApiController
{
    private const int _defaultTake = 50;
    private const int _maxTake = 100;

    private readonly IContentRequestService _contentRequestService;
    private readonly IContentRequestWebPushService _contentRequestWebPushService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestController"/> class.
    /// </summary>
    /// <param name="contentRequestService">Content request service.</param>
    /// <param name="contentRequestWebPushService">Content request web push service.</param>
    public RequestController(IContentRequestService contentRequestService, IContentRequestWebPushService contentRequestWebPushService)
    {
        _contentRequestService = contentRequestService;
        _contentRequestWebPushService = contentRequestWebPushService;
    }

    /// <summary>
    /// Creates a content request.
    /// </summary>
    /// <param name="request">Create request payload.</param>
    /// <returns>Created request row.</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContentRequestRowDto>> CreateRequest([FromBody, Required] CreateContentRequestRequest request)
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        try
        {
            var row = await _contentRequestService
                .CreateRequest(userId, request.Title, request.Type, request.SeasonNumber)
                .ConfigureAwait(false);

            return ToDto(row);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ContentRequestInactiveSubscriptionException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (ContentRequestConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Gets the VAPID public key used for browser push subscriptions.
    /// </summary>
    /// <returns>The VAPID public key payload.</returns>
    [HttpGet("WebPush/PublicKey")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<RequestWebPushPublicKeyResponse> GetWebPushPublicKey()
    {
        var publicKey = _contentRequestWebPushService.GetPublicVapidKey();
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            return NotFound();
        }

        return new RequestWebPushPublicKeyResponse
        {
            PublicKey = publicKey
        };
    }

    /// <summary>
    /// Creates or updates a browser push subscription for the current user.
    /// </summary>
    /// <param name="request">Subscription payload.</param>
    /// <returns>No content.</returns>
    [HttpPost("WebPush/Subscribe")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SubscribeWebPush([FromBody, Required] RequestWebPushSubscriptionRequest request)
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        try
        {
            await _contentRequestWebPushService
                .UpsertSubscription(userId, request.Endpoint, request.P256dh, request.Auth)
                .ConfigureAwait(false);

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Removes a browser push subscription for the current user.
    /// </summary>
    /// <param name="request">Unsubscribe payload.</param>
    /// <returns>No content.</returns>
    [HttpPost("WebPush/Unsubscribe")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UnsubscribeWebPush([FromBody, Required] RequestWebPushUnsubscribeRequest request)
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        await _contentRequestWebPushService
            .RemoveSubscription(userId, request.Endpoint)
            .ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// Gets current user request rows and cap summary.
    /// </summary>
    /// <returns>My requests response.</returns>
    [HttpGet("My")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MyContentRequestsResponse>> GetMyRequests()
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        try
        {
            var response = await _contentRequestService.GetMyRequests(userId).ConfigureAwait(false);
            return ToDto(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Gets paged current user request rows and cap summary.
    /// </summary>
    /// <param name="skip">Rows to skip.</param>
    /// <param name="take">Rows to take.</param>
    /// <returns>Paged my requests response.</returns>
    [HttpGet("My/Paged")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MyContentRequestsPagedResponse>> GetMyRequestsPaged([FromQuery] int skip = 0, [FromQuery] int take = 10)
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        var normalizedSkip = Math.Max(0, skip);
        var normalizedTake = take <= 0 ? 10 : Math.Min(take, _maxTake);

        try
        {
            var list = await _contentRequestService.GetMyRequestsPaged(userId, normalizedSkip, normalizedTake).ConfigureAwait(false);
            var summary = await _contentRequestService.GetMyRequests(userId).ConfigureAwait(false);
            return new MyContentRequestsPagedResponse
            {
                Items = list.Items.Select(ToDto).ToList(),
                TotalRecordCount = list.TotalRecordCount,
                Quota = ToQuotaDto(summary.Quota)
            };
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Gets public content requests.
    /// </summary>
    /// <param name="skip">Rows to skip.</param>
    /// <param name="take">Rows to take.</param>
    /// <returns>Public request rows.</returns>
    [HttpGet("Public")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicContentRequestListResponse>> GetPublicRequests([FromQuery] int skip = 0, [FromQuery] int take = _defaultTake)
    {
        var normalizedSkip = Math.Max(0, skip);
        var normalizedTake = take <= 0 ? _defaultTake : Math.Min(take, _maxTake);

        var response = await _contentRequestService.GetPublicRequests(normalizedSkip, normalizedTake).ConfigureAwait(false);
        return ToDto(response);
    }

    /// <summary>
    /// Searches users for admin reward assignment suggestions.
    /// </summary>
    /// <param name="query">Search text.</param>
    /// <param name="take">Maximum suggestion rows.</param>
    /// <returns>User suggestions.</returns>
    [HttpGet("Admin/UserSuggestions")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AdminContentRequestUserSuggestionDto>>> GetAdminUserSuggestions([FromQuery] string query, [FromQuery] int take = 8)
    {
        try
        {
            var rows = await _contentRequestService.SearchUsersForAdmin(query, take).ConfigureAwait(false);
            return rows.Select(ToDto).ToList();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Gets quota details for a target user as admin.
    /// </summary>
    /// <param name="userId">Target user id.</param>
    /// <returns>User quota details.</returns>
    [HttpGet("Admin/UserQuota")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminContentRequestUserQuotaResponse>> GetAdminUserQuota([FromQuery, Required] Guid userId)
    {
        try
        {
            var result = await _contentRequestService.GetAdminUserQuota(userId).ConfigureAwait(false);
            return ToDto(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ContentRequestNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Grants additional rewarded request slots to a user.
    /// </summary>
    /// <param name="request">Reward grant payload.</param>
    /// <returns>Updated user quota details.</returns>
    [HttpPost("Admin/RewardQuota")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminContentRequestUserQuotaResponse>> RewardQuota([FromBody, Required] AdminRewardContentRequestQuotaRequest request)
    {
        try
        {
            var result = await _contentRequestService
                .GrantAdminRewardQuota(request.UserId, request.MovieCount, request.SeriesCount)
                .ConfigureAwait(false);

            return ToDto(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ContentRequestNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Gets completion notifications for current user.
    /// </summary>
    /// <returns>Notification rows.</returns>
    [HttpGet("Notifications")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ContentRequestRowDto>>> GetNotifications()
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        try
        {
            var rows = await _contentRequestService.GetNotifications(userId).ConfigureAwait(false);
            return rows.Select(ToDto).ToList();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Marks notification rows viewed by incrementing notification count.
    /// </summary>
    /// <param name="request">Bulk viewed request payload.</param>
    /// <returns>No content.</returns>
    [HttpPost("NotificationViewedBulk")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> BulkMarkNotificationViewed([FromBody, Required] BulkNotificationViewedRequest request)
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        try
        {
            await _contentRequestService
                .BulkMarkNotificationViewed(userId, request.RequestIds ?? [])
                .ConfigureAwait(false);

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Gets admin content request rows and marks unseen pending rows as viewed.
    /// </summary>
    /// <returns>Admin request rows.</returns>
    [HttpGet("Admin")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ContentRequestRowDto>>> GetAdminRequests()
    {
        var rows = await _contentRequestService.GetAdminRequests().ConfigureAwait(false);
        return rows.Select(ToDto).ToList();
    }

    /// <summary>
    /// Gets paged admin request rows and marks unseen pending rows as viewed.
    /// </summary>
    /// <param name="skip">Rows to skip.</param>
    /// <param name="take">Rows to take.</param>
    /// <returns>Paged admin request rows.</returns>
    [HttpGet("Admin/Paged")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminContentRequestListResponse>> GetAdminRequestsPaged([FromQuery] int skip = 0, [FromQuery] int take = 10)
    {
        var normalizedSkip = Math.Max(0, skip);
        var normalizedTake = take <= 0 ? 10 : Math.Min(take, _maxTake);
        var rows = await _contentRequestService.GetAdminRequestsPaged(normalizedSkip, normalizedTake).ConfigureAwait(false);
        return new AdminContentRequestListResponse
        {
            Items = rows.Items.Select(ToDto).ToList(),
            TotalRecordCount = rows.TotalRecordCount
        };
    }

    /// <summary>
    /// Gets count of admin unseen pending requests.
    /// </summary>
    /// <returns>Unseen pending count payload.</returns>
    [HttpGet("Admin/UnseenPendingCount")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminUnseenPendingCountResponse>> GetAdminUnseenPendingCount()
    {
        var count = await _contentRequestService.GetAdminUnseenPendingCount().ConfigureAwait(false);
        return new AdminUnseenPendingCountResponse
        {
            Count = count
        };
    }

    /// <summary>
    /// Approves a pending request.
    /// </summary>
    /// <param name="request">Action payload.</param>
    /// <returns>Updated request row.</returns>
    [HttpPost("Admin/Approve")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContentRequestRowDto>> Approve([FromBody, Required] AdminRequestActionRequest request)
    {
        try
        {
            var row = await _contentRequestService.Approve(request.RequestId).ConfigureAwait(false);
            return ToDto(row);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ContentRequestNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ContentRequestConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Rejects a request.
    /// </summary>
    /// <param name="request">Action payload.</param>
    /// <returns>Updated request row.</returns>
    [HttpPost("Admin/Reject")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContentRequestRowDto>> Reject([FromBody, Required] AdminRequestActionRequest request)
    {
        try
        {
            var row = await _contentRequestService.Reject(request.RequestId).ConfigureAwait(false);
            return ToDto(row);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ContentRequestNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ContentRequestConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Completes an approved request by linking a Jellyfin item id.
    /// </summary>
    /// <param name="request">Complete payload.</param>
    /// <returns>Updated request row.</returns>
    [HttpPost("Admin/Complete")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContentRequestRowDto>> Complete([FromBody, Required] AdminCompleteContentRequestRequest request)
    {
        try
        {
            var row = await _contentRequestService
                .Complete(request.RequestId, request.JellyfinItemId)
                .ConfigureAwait(false);

            await _contentRequestWebPushService.NotifyRequestCompleted(row).ConfigureAwait(false);

            return ToDto(row);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ContentRequestNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ContentRequestConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    private static ContentRequestRowDto ToDto(ContentRequestInfo row)
        => new()
        {
            Id = row.Id,
            UserId = row.UserId,
            Username = row.Username,
            Title = row.Title,
            Type = row.Type,
            SeasonNumber = row.SeasonNumber,
            RequestedAt = row.RequestedAt,
            Status = row.Status,
            JellyfinItemId = row.JellyfinItemId,
            NotificationCount = row.NotificationCount,
            IsAdminViewed = row.IsAdminViewed
        };

    private static PublicContentRequestRowDto ToPublicDto(ContentRequestInfo row)
        => new()
        {
            Id = row.Id,
            UserId = row.UserId,
            Username = row.Username ?? string.Empty,
            Title = row.Title,
            Type = row.Type,
            SeasonNumber = row.SeasonNumber,
            RequestedAt = row.RequestedAt,
            Status = row.Status,
            JellyfinItemId = row.JellyfinItemId
        };

    private static PublicContentRequestListResponse ToDto(ContentRequestListResult result)
        => new()
        {
            Items = result.Items.Select(ToPublicDto).ToList(),
            TotalRecordCount = result.TotalRecordCount
        };

    private static MyContentRequestsResponse ToDto(MyContentRequestsResult result)
        => new()
        {
            Requests = result.Requests.Select(ToDto).ToList(),
            Quota = ToQuotaDto(result.Quota)
        };

    private static AdminContentRequestUserSuggestionDto ToDto(ContentRequestUserSuggestion row)
        => new()
        {
            UserId = row.UserId,
            Username = row.Username
        };

    private static AdminContentRequestUserQuotaResponse ToDto(ContentRequestAdminUserQuotaResult result)
        => new()
        {
            UserId = result.UserId,
            Username = result.Username,
            Quota = ToQuotaDto(result.Quota)
        };

    private static ContentRequestCapSummaryDto ToQuotaDto(ContentRequestQuotaInfo quota)
        => new()
        {
            CycleStartDate = quota.CycleStartDate,
            IsSubscriptionActive = quota.IsSubscriptionActive,
            MovieCap = quota.MovieCap,
            SeriesCap = quota.SeriesCap,
            UsedMovies = quota.UsedMovies,
            UsedSeries = quota.UsedSeries,
            RemainingMovies = quota.RemainingMovies,
            RemainingSeries = quota.RemainingSeries,
            RewardMovies = quota.RewardMovies,
            RewardSeries = quota.RewardSeries
        };
}
