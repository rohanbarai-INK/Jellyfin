using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Models.AccessKeyDtos;
using Jellyfin.Database.Implementations;
using Jellyfin.Extensions;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Access key controller.
/// </summary>
[Route("Keys")]
public class AccessKeyController : BaseJellyfinApiController
{
    private readonly IAccessKeyService _accessKeyService;
    private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessKeyController"/> class.
    /// </summary>
    /// <param name="accessKeyService">Instance of <see cref="IAccessKeyService"/>.</param>
    /// <param name="dbProvider">Instance of <see cref="IDbContextFactory{JellyfinDbContext}"/>.</param>
    public AccessKeyController(
        IAccessKeyService accessKeyService,
        IDbContextFactory<JellyfinDbContext> dbProvider)
    {
        _accessKeyService = accessKeyService;
        _dbProvider = dbProvider;
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
    /// Generates access keys in bulk.
    /// </summary>
    /// <param name="request">Bulk generate request.</param>
    /// <response code="200">Access keys generated.</response>
    /// <response code="400">Invalid request.</response>
    /// <returns>A <see cref="GenerateAccessKeysBulkResponse"/>.</returns>
    [HttpPost("GenerateBulk")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GenerateAccessKeysBulkResponse>> GenerateKeysBulk(
        [FromBody, Required] GenerateAccessKeysBulkRequest request)
    {
        if (request.Quantity < 1 || request.Quantity > 1000)
        {
            return BadRequest("Quantity must be between 1 and 1000.");
        }

        var items = new List<GenerateAccessKeyResponse>(request.Quantity);
        try
        {
            for (var index = 0; index < request.Quantity; index++)
            {
                var result = await _accessKeyService.GenerateKey(request.DurationMonths).ConfigureAwait(false);
                items.Add(new GenerateAccessKeyResponse
                {
                    Key = result.Key,
                    DurationMonths = result.DurationMonths,
                    CreatedAt = result.CreatedAt
                });
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }

        return new GenerateAccessKeysBulkResponse
        {
            Items = items
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

    /// <summary>
    /// Gets admin dashboard analytics for subscriptions and access keys.
    /// </summary>
    /// <response code="200">Analytics returned.</response>
    /// <returns>A <see cref="AdminSubscriptionDashboardResponse"/>.</returns>
    [HttpGet("AdminDashboard")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminSubscriptionDashboardResponse>> GetAdminDashboard()
    {
        var nowUtc = DateTime.UtcNow;
        var users = await GetDashboardUsers().ConfigureAwait(false);
        var accessKeys = await GetDashboardAccessKeys().ConfigureAwait(false);

        var redeemedKeys = accessKeys
            .Where(accessKey => accessKey.IsRedeemed && accessKey.RedeemedByUserId.HasValue)
            .ToList();

        var groupedRedeemedKeys = redeemedKeys
            .GroupBy(accessKey => accessKey.RedeemedByUserId!.Value)
            .Select(group => group
                .OrderBy(GetRedeemedAtOrCreatedAt)
                .ThenBy(accessKey => accessKey.CreatedAt)
                .ToList())
            .ToList();

        var activeUsers = users.Count(user => !user.ExpiryDate.HasValue || user.ExpiryDate.Value > nowUtc);
        var graceUsers = users.Count(user => user.ExpiryDate.HasValue && _accessKeyService.IsWithinGracePeriod(user.ExpiryDate));
        var expiredUsers = users.Count(user => user.ExpiryDate.HasValue
            && user.ExpiryDate.Value <= nowUtc
            && !_accessKeyService.IsWithinGracePeriod(user.ExpiryDate));

        var keysGenerated = accessKeys.Count;
        var keysRedeemed = redeemedKeys.Count;
        var unusedKeys = keysGenerated - keysRedeemed;
        var expiredKeyCycles = redeemedKeys.Count(accessKey => ResolveCycleEndDate(accessKey) < nowUtc);
        var totalRevenue = redeemedKeys.Sum(accessKey => accessKey.RedeemedAmount ?? 0m);

        var overview = new AdminSubscriptionOverviewResponse
        {
            ActiveUsers = activeUsers,
            GraceUsers = graceUsers,
            ExpiredUsers = expiredUsers,
            ExpiringSoon = CountUsersExpiringWithin(users, nowUtc, 7),
            TotalRevenue = totalRevenue,
            KeysGenerated = keysGenerated,
            KeysRedeemed = keysRedeemed,
            UnusedKeys = unusedKeys
        };

        var expiryRadar = new AdminSubscriptionExpiryRadarResponse
        {
            Next24h = CountUsersExpiringWithin(users, nowUtc, 1),
            Next3days = CountUsersExpiringWithin(users, nowUtc, 3),
            Next7days = CountUsersExpiringWithin(users, nowUtc, 7),
            Next30days = CountUsersExpiringWithin(users, nowUtc, 30)
        };

        var keyStats = new AdminSubscriptionKeyStatsResponse
        {
            TotalGenerated = keysGenerated,
            Redeemed = keysRedeemed,
            Unused = unusedKeys,
            Expired = expiredKeyCycles
        };

        var cohorts = BuildCohorts(groupedRedeemedKeys, nowUtc);
        var renewalRate = CalculateRollingRenewalRate(groupedRedeemedKeys, nowUtc.AddDays(-30));

        var thirtyDaysAgoUtc = nowUtc.AddDays(-30);
        var activeUsersThirtyDaysAgo = users.Count(user => !user.ExpiryDate.HasValue || user.ExpiryDate.Value > thirtyDaysAgoUtc);
        var monthlyGrowth = activeUsersThirtyDaysAgo == 0
            ? (activeUsers > 0 ? 100 : 0)
            : (int)Math.Round(((activeUsers - activeUsersThirtyDaysAgo) / (double)activeUsersThirtyDaysAgo) * 100);

        return new AdminSubscriptionDashboardResponse
        {
            Overview = overview,
            ExpiryRadar = expiryRadar,
            KeyStats = keyStats,
            Cohorts = cohorts,
            SystemHealth = new AdminSubscriptionSystemHealthResponse
            {
                RenewalRate = renewalRate,
                ActiveUsers = activeUsers,
                MonthlyGrowth = monthlyGrowth
            }
        };
    }

    /// <summary>
    /// Gets users expiring inside a future day window.
    /// </summary>
    /// <param name="days">Window size in days.</param>
    /// <response code="200">Rows returned.</response>
    /// <response code="400">Invalid day window.</response>
    /// <returns>Expiring users list.</returns>
    [HttpGet("AdminExpiringUsers")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AdminSubscriptionExpiringUserResponse>>> GetAdminExpiringUsers([FromQuery] int days = 7)
    {
        if (days < 1 || days > 365)
        {
            return BadRequest("Days must be between 1 and 365.");
        }

        var nowUtc = DateTime.UtcNow;
        var upperBoundUtc = nowUtc.AddDays(days);

        var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            var expiringUsers = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.ExpiryDate.HasValue
                    && user.ExpiryDate.Value > nowUtc
                    && user.ExpiryDate.Value <= upperBoundUtc)
                .Select(user => new AdminDashboardUserProjection
                {
                    Id = user.Id,
                    Username = user.Username,
                    ExpiryDate = user.ExpiryDate
                })
                .ToListAsync()
                .ConfigureAwait(false);

            if (expiringUsers.Count == 0)
            {
                return Array.Empty<AdminSubscriptionExpiringUserResponse>();
            }

            var userIds = expiringUsers.Select(user => user.Id).ToHashSet();
            var redeemedKeys = await dbContext.AccessKeys
                .AsNoTracking()
                .Where(accessKey => accessKey.IsRedeemed
                    && accessKey.RedeemedByUserId.HasValue
                    && userIds.Contains(accessKey.RedeemedByUserId.Value))
                .Select(accessKey => new AdminDashboardKeyProjection
                {
                    IsRedeemed = accessKey.IsRedeemed,
                    RedeemedByUserId = accessKey.RedeemedByUserId,
                    DurationMonths = accessKey.DurationMonths,
                    CreatedAt = accessKey.CreatedAt,
                    RedeemedAt = accessKey.RedeemedAt,
                    RedeemedAmount = accessKey.RedeemedAmount,
                    CycleStartDate = accessKey.CycleStartDate,
                    CycleEndDate = accessKey.CycleEndDate
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var latestDurationByUserId = redeemedKeys
                .Where(accessKey => accessKey.RedeemedByUserId.HasValue)
                .GroupBy(accessKey => accessKey.RedeemedByUserId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(GetRedeemedAtOrCreatedAt)
                        .ThenByDescending(accessKey => accessKey.CreatedAt)
                        .First()
                        .DurationMonths);

            var rows = expiringUsers
                .Where(user => user.ExpiryDate.HasValue)
                .OrderBy(user => user.ExpiryDate!.Value)
                .Select(user =>
                {
                    var expiryDateUtc = user.ExpiryDate!.Value;
                    var daysRemaining = Math.Max(0, (int)Math.Floor((expiryDateUtc - nowUtc).TotalDays));
                    var hasDuration = latestDurationByUserId.TryGetValue(user.Id, out var durationMonths);

                    return new AdminSubscriptionExpiringUserResponse
                    {
                        UserId = user.Id.ToString("N", CultureInfo.InvariantCulture),
                        Username = user.Username,
                        ExpiryDate = expiryDateUtc,
                        DaysRemaining = daysRemaining,
                        Plan = hasDuration ? DurationMonthsToLabel(durationMonths) : "N/A"
                    };
                })
                .ToList();

            return rows;
        }
    }

    private async Task<List<AdminDashboardUserProjection>> GetDashboardUsers()
    {
        var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            return await dbContext.Users
                .AsNoTracking()
                .Select(user => new AdminDashboardUserProjection
                {
                    Id = user.Id,
                    Username = user.Username,
                    ExpiryDate = user.ExpiryDate
                })
                .ToListAsync()
                .ConfigureAwait(false);
        }
    }

    private async Task<List<AdminDashboardKeyProjection>> GetDashboardAccessKeys()
    {
        var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            return await dbContext.AccessKeys
                .AsNoTracking()
                .Select(accessKey => new AdminDashboardKeyProjection
                {
                    IsRedeemed = accessKey.IsRedeemed,
                    RedeemedByUserId = accessKey.RedeemedByUserId,
                    DurationMonths = accessKey.DurationMonths,
                    CreatedAt = accessKey.CreatedAt,
                    RedeemedAt = accessKey.RedeemedAt,
                    RedeemedAmount = accessKey.RedeemedAmount,
                    CycleStartDate = accessKey.CycleStartDate,
                    CycleEndDate = accessKey.CycleEndDate
                })
                .ToListAsync()
                .ConfigureAwait(false);
        }
    }

    private static int CountUsersExpiringWithin(IEnumerable<AdminDashboardUserProjection> users, DateTime nowUtc, int days)
    {
        var horizonUtc = nowUtc.AddDays(days);
        return users.Count(user => user.ExpiryDate.HasValue
            && user.ExpiryDate.Value > nowUtc
            && user.ExpiryDate.Value <= horizonUtc);
    }

    private static int CalculateRollingRenewalRate(
        IReadOnlyCollection<List<AdminDashboardKeyProjection>> groupedRedeemedKeys,
        DateTime cutoffUtc)
    {
        var recentRedemptions = 0;
        var recentRenewals = 0;

        foreach (var userKeys in groupedRedeemedKeys)
        {
            for (var index = 0; index < userKeys.Count; index++)
            {
                var redeemedAtUtc = GetRedeemedAtOrCreatedAt(userKeys[index]);
                if (redeemedAtUtc < cutoffUtc)
                {
                    continue;
                }

                recentRedemptions++;
                if (index > 0)
                {
                    recentRenewals++;
                }
            }
        }

        return recentRedemptions == 0
            ? 0
            : (int)Math.Round((recentRenewals / (double)recentRedemptions) * 100);
    }

    private static IReadOnlyList<AdminSubscriptionCohortResponse> BuildCohorts(
        IReadOnlyCollection<List<AdminDashboardKeyProjection>> groupedRedeemedKeys,
        DateTime nowUtc)
    {
        var currentMonthStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cohorts = new List<AdminSubscriptionCohortResponse>(7);

        for (var offset = 6; offset >= 0; offset--)
        {
            var monthStartUtc = currentMonthStartUtc.AddMonths(-offset);
            var monthEndUtc = monthStartUtc.AddMonths(1);

            var joinedGroups = groupedRedeemedKeys
                .Where(userKeys =>
                {
                    var firstRedeemedAtUtc = GetRedeemedAtOrCreatedAt(userKeys[0]);
                    return firstRedeemedAtUtc >= monthStartUtc && firstRedeemedAtUtc < monthEndUtc;
                })
                .ToList();

            var usersJoined = joinedGroups.Count;
            var renewedUsers = joinedGroups.Count(userKeys => userKeys.Count > 1);
            var renewalRate = usersJoined == 0
                ? 0
                : (int)Math.Round((renewedUsers / (double)usersJoined) * 100);

            cohorts.Add(new AdminSubscriptionCohortResponse
            {
                Month = monthStartUtc.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                UsersJoined = usersJoined,
                RenewalRate = renewalRate
            });
        }

        return cohorts;
    }

    private static DateTime GetRedeemedAtOrCreatedAt(AdminDashboardKeyProjection accessKey)
        => accessKey.RedeemedAt ?? accessKey.CreatedAt;

    private static DateTime ResolveCycleEndDate(AdminDashboardKeyProjection accessKey)
    {
        if (accessKey.CycleEndDate.HasValue)
        {
            return accessKey.CycleEndDate.Value;
        }

        var cycleStartUtc = accessKey.CycleStartDate ?? GetRedeemedAtOrCreatedAt(accessKey);
        return CalculateUpdatedExpiryDate(cycleStartUtc, accessKey.DurationMonths);
    }

    private static DateTime CalculateUpdatedExpiryDate(DateTime redeemedAtUtc, int durationMonths)
    {
        var monthBasedExpiry = redeemedAtUtc.AddMonths(durationMonths);
        if (redeemedAtUtc.Month != 2)
        {
            return monthBasedExpiry;
        }

        var minimumFairExpiry = redeemedAtUtc.AddDays(durationMonths * 30);
        return monthBasedExpiry >= minimumFairExpiry ? monthBasedExpiry : minimumFairExpiry;
    }

    private static string DurationMonthsToLabel(int months)
        => months switch
        {
            1 => "1 Month",
            _ => $"{months} Months"
        };

    private sealed class AdminDashboardUserProjection
    {
        public Guid Id { get; init; }

        public required string Username { get; init; }

        public DateTime? ExpiryDate { get; init; }
    }

    private sealed class AdminDashboardKeyProjection
    {
        public bool IsRedeemed { get; init; }

        public Guid? RedeemedByUserId { get; init; }

        public int DurationMonths { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime? RedeemedAt { get; init; }

        public decimal? RedeemedAmount { get; init; }

        public DateTime? CycleStartDate { get; init; }

        public DateTime? CycleEndDate { get; init; }
    }
}
