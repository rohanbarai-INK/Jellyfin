using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Models.AccessKeyDtos;
using Jellyfin.Extensions;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Access key controller.
/// </summary>
[Route("Keys")]
public class AccessKeyController : BaseJellyfinApiController
{
    private readonly IAccessKeyService _accessKeyService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessKeyController"/> class.
    /// </summary>
    /// <param name="accessKeyService">Instance of <see cref="IAccessKeyService"/>.</param>
    public AccessKeyController(IAccessKeyService accessKeyService)
    {
        _accessKeyService = accessKeyService;
    }

    /// <summary>
    /// Generates a new access key.
    /// </summary>
    /// <param name="request">The create request.</param>
    /// <response code="200">Access key generated.</response>
    /// <response code="400">Invalid request.</response>
    /// <returns>A <see cref="GenerateAccessKeyResponse"/>.</returns>
    [HttpPost("Generate")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GenerateAccessKeyResponse>> GenerateKey(
        [FromBody, Required] GenerateAccessKeyRequest request)
    {
        var result = await _accessKeyService.GenerateKey(request.DurationMonths).ConfigureAwait(false);
        return new GenerateAccessKeyResponse
        {
            Key = result.Key,
            DurationMonths = result.DurationMonths,
            CreatedAt = result.CreatedAt
        };
    }

    /// <summary>
    /// Redeems an access key for the current user.
    /// </summary>
    /// <param name="request">The redeem request.</param>
    /// <response code="200">Key redeemed.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="404">Key not found.</response>
    /// <response code="409">Key already redeemed.</response>
    /// <returns>A <see cref="RedeemAccessKeyResponse"/>.</returns>
    [HttpPost("Redeem")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RedeemAccessKeyResponse>> RedeemKey(
        [FromBody, Required] RedeemAccessKeyRequest request)
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        try
        {
            var result = await _accessKeyService.RedeemKey(userId, request.Key).ConfigureAwait(false);
            return new RedeemAccessKeyResponse
            {
                ExpiryDate = result.ExpiryDate,
                DurationMonths = result.DurationMonths,
                RedeemedAt = result.RedeemedAt
            };
        }
        catch (AccessKeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (AccessKeyAlreadyRedeemedException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Gets current subscription metadata for the authenticated user.
    /// </summary>
    /// <response code="200">Subscription metadata returned.</response>
    /// <response code="400">User is not authenticated.</response>
    /// <returns>A <see cref="CurrentSubscriptionResponse"/>.</returns>
    [HttpGet("CurrentSubscription")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CurrentSubscriptionResponse>> GetCurrentSubscription()
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        var result = await _accessKeyService.GetCurrentSubscription(userId).ConfigureAwait(false);
        return new CurrentSubscriptionResponse
        {
            ExpiryDate = result.ExpiryDate,
            Status = result.Status,
            IsInGracePeriod = result.IsInGracePeriod,
            GraceDaysRemaining = result.GraceDaysRemaining,
            LastDurationMonths = result.LastDurationMonths,
            LastRedeemedAt = result.LastRedeemedAt
        };
    }

    /// <summary>
    /// Gets immutable billing history records for the authenticated user.
    /// </summary>
    /// <response code="200">Billing history returned.</response>
    /// <response code="400">User is not authenticated.</response>
    /// <returns>A <see cref="BillingHistoryResponse"/>.</returns>
    [HttpGet("BillingHistory")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BillingHistoryResponse>> GetBillingHistory()
    {
        var userId = User.GetUserId();
        if (userId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        var items = await _accessKeyService.GetBillingHistory(userId).ConfigureAwait(false);
        return new BillingHistoryResponse
        {
            Items = items.Select(item => new BillingHistoryEntryResponse
            {
                Reference = item.Reference,
                DurationMonths = item.DurationMonths,
                CycleStartDate = item.CycleStartDate,
                CycleEndDate = item.CycleEndDate,
                RedeemedAt = item.RedeemedAt,
                Amount = item.Amount,
                Status = item.Status
            }).ToList()
        };
    }
}
