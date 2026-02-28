using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Extensions;
using MediaBrowser.Controller.ContentRequests;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Server.Implementations.ContentRequests
{
    /// <inheritdoc />
    public class ContentRequestService : IContentRequestService
    {
        private const int _movieCap = 5;
        private const int _seriesCap = 2;

        private static readonly Regex _whitespaceRegex = new(@"\s+", RegexOptions.Compiled);

        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentRequestService"/> class.
        /// </summary>
        /// <param name="dbProvider">Database provider.</param>
        public ContentRequestService(IDbContextFactory<JellyfinDbContext> dbProvider)
        {
            _dbProvider = dbProvider;
        }

        /// <inheritdoc />
        public async Task<ContentRequestInfo> CreateRequest(Guid userId, string title, ContentRequestType type, int? seasonNumber)
        {
            if (userId.IsEmpty())
            {
                throw new ArgumentException("User id cannot be empty.", nameof(userId));
            }

            var normalizedTitle = NormalizeTitle(title);
            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                throw new ArgumentException("Title is required.", nameof(title));
            }

            var trimmedTitle = title.Trim();
            if (trimmedTitle.Length > 255)
            {
                throw new ArgumentException("Title exceeds maximum length of 255.", nameof(title));
            }

            ValidateType(type);
            var normalizedSeasonNumber = NormalizeSeasonNumber(type, seasonNumber);
            var dbType = ToDatabaseType(type);
            var nowUtc = DateTime.UtcNow;

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var transaction = await dbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
                await using (transaction.ConfigureAwait(false))
                {
                    var cycleInfo = await ResolveSubscriptionCycleAsync(dbContext, userId, nowUtc).ConfigureAwait(false);
                    if (!cycleInfo.IsSubscriptionActive)
                    {
                        throw new ContentRequestInactiveSubscriptionException("Subscription is inactive.");
                    }

                    var usedCounts = await GetCycleUsageAsync(dbContext, userId, cycleInfo.CycleStartDate).ConfigureAwait(false);
                    if (dbType == Jellyfin.Database.Implementations.Enums.ContentRequestType.Movie
                        && usedCounts.UsedMovies >= _movieCap)
                    {
                        throw new ContentRequestConflictException("Movie request cap reached for the current subscription cycle.");
                    }

                    if (dbType == Jellyfin.Database.Implementations.Enums.ContentRequestType.Series
                        && usedCounts.UsedSeries >= _seriesCap)
                    {
                        throw new ContentRequestConflictException("Series request cap reached for the current subscription cycle.");
                    }

                    var duplicateExists = await dbContext.ContentRequests
                        .AnyAsync(request => request.NormalizedTitle == normalizedTitle
                            && request.Type == dbType
                            && (request.Status == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Pending
                                || request.Status == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Approved))
                        .ConfigureAwait(false);

                    if (duplicateExists)
                    {
                        throw new ContentRequestConflictException("An active request with the same title and type already exists.");
                    }

                    var requestEntity = new ContentRequest
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Title = trimmedTitle,
                        NormalizedTitle = normalizedTitle,
                        Type = dbType,
                        SeasonNumber = normalizedSeasonNumber,
                        RequestedAt = nowUtc,
                        Status = Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Pending,
                        JellyfinItemId = null,
                        NotificationCount = 0,
                        IsAdminViewed = false
                    };

                    dbContext.ContentRequests.Add(requestEntity);
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                    await transaction.CommitAsync().ConfigureAwait(false);

                    return ToContractModel(requestEntity);
                }
            }
        }

        /// <inheritdoc />
        public async Task<MyContentRequestsResult> GetMyRequests(Guid userId)
        {
            if (userId.IsEmpty())
            {
                throw new ArgumentException("User id cannot be empty.", nameof(userId));
            }

            var nowUtc = DateTime.UtcNow;
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var cycleInfo = await ResolveSubscriptionCycleAsync(dbContext, userId, nowUtc).ConfigureAwait(false);
                var usage = cycleInfo.IsSubscriptionActive
                    ? await GetCycleUsageAsync(dbContext, userId, cycleInfo.CycleStartDate).ConfigureAwait(false)
                    : (UsedMovies: 0, UsedSeries: 0);

                var rows = await dbContext.ContentRequests
                    .AsNoTracking()
                    .Where(request => request.UserId.Equals(userId))
                    .OrderByDescending(request => request.RequestedAt)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new MyContentRequestsResult
                {
                    Requests = rows.Select(row => ToContractModel(row)).ToList(),
                    Quota = new ContentRequestQuotaInfo
                    {
                        CycleStartDate = cycleInfo.CycleStartDate,
                        IsSubscriptionActive = cycleInfo.IsSubscriptionActive,
                        MovieCap = _movieCap,
                        SeriesCap = _seriesCap,
                        UsedMovies = usage.UsedMovies,
                        UsedSeries = usage.UsedSeries,
                        RemainingMovies = cycleInfo.IsSubscriptionActive ? Math.Max(0, _movieCap - usage.UsedMovies) : 0,
                        RemainingSeries = cycleInfo.IsSubscriptionActive ? Math.Max(0, _seriesCap - usage.UsedSeries) : 0
                    }
                };
            }
        }

        /// <inheritdoc />
        public async Task<ContentRequestListResult> GetPublicRequests(int skip, int take)
        {
            if (skip < 0)
            {
                skip = 0;
            }

            if (take < 0)
            {
                take = 0;
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var publicQuery = dbContext.ContentRequests
                    .AsNoTracking()
                    .Where(request => request.Status == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Pending
                        || request.Status == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Approved
                        || request.Status == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Completed);

                var totalRecordCount = await publicQuery.CountAsync().ConfigureAwait(false);
                var rows = await publicQuery
                    .OrderByDescending(request => request.RequestedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new ContentRequestListResult
                {
                    TotalRecordCount = totalRecordCount,
                    Items = rows.Select(row => ToContractModel(row)).ToList()
                };
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ContentRequestInfo>> GetAdminRequests()
        {
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var unseenPendingRows = await dbContext.ContentRequests
                    .Where(request => request.Status == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Pending
                        && !request.IsAdminViewed)
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (unseenPendingRows.Count > 0)
                {
                    foreach (var unseenPendingRow in unseenPendingRows)
                    {
                        unseenPendingRow.IsAdminViewed = true;
                    }

                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                }

                var rows = await dbContext.ContentRequests
                    .AsNoTracking()
                    .Include(request => request.User)
                    .OrderByDescending(request => request.RequestedAt)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return rows
                    .Select(request => ToContractModel(request, request.User?.Username))
                    .ToList();
            }
        }

        /// <inheritdoc />
        public async Task<int> GetAdminUnseenPendingCount()
        {
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                return await dbContext.ContentRequests
                    .AsNoTracking()
                    .CountAsync(request => request.Status == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Pending
                        && !request.IsAdminViewed)
                    .ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<ContentRequestInfo> Approve(Guid requestId)
        {
            if (requestId.IsEmpty())
            {
                throw new ArgumentException("Request id cannot be empty.", nameof(requestId));
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var row = await dbContext.ContentRequests
                    .FirstOrDefaultAsync(request => request.Id.Equals(requestId))
                    .ConfigureAwait(false)
                    ?? throw new ContentRequestNotFoundException("Request not found.");

                EnsureTransitionAllowed(row.Status, Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Approved);

                row.Status = Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Approved;
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                return ToContractModel(row);
            }
        }

        /// <inheritdoc />
        public async Task<ContentRequestInfo> Reject(Guid requestId)
        {
            if (requestId.IsEmpty())
            {
                throw new ArgumentException("Request id cannot be empty.", nameof(requestId));
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var row = await dbContext.ContentRequests
                    .FirstOrDefaultAsync(request => request.Id.Equals(requestId))
                    .ConfigureAwait(false)
                    ?? throw new ContentRequestNotFoundException("Request not found.");

                EnsureTransitionAllowed(row.Status, Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Rejected);

                row.Status = Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Rejected;
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                return ToContractModel(row);
            }
        }

        /// <inheritdoc />
        public async Task<ContentRequestInfo> Complete(Guid requestId, Guid jellyfinItemId)
        {
            if (requestId.IsEmpty())
            {
                throw new ArgumentException("Request id cannot be empty.", nameof(requestId));
            }

            if (jellyfinItemId.IsEmpty())
            {
                throw new ArgumentException("Jellyfin item id is required.", nameof(jellyfinItemId));
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var row = await dbContext.ContentRequests
                    .FirstOrDefaultAsync(request => request.Id.Equals(requestId))
                    .ConfigureAwait(false)
                    ?? throw new ContentRequestNotFoundException("Request not found.");

                EnsureTransitionAllowed(row.Status, Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Completed);

                row.Status = Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Completed;
                row.JellyfinItemId = jellyfinItemId;
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                return ToContractModel(row);
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ContentRequestInfo>> GetNotifications(Guid userId)
        {
            if (userId.IsEmpty())
            {
                throw new ArgumentException("User id cannot be empty.", nameof(userId));
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var rows = await dbContext.ContentRequests
                    .AsNoTracking()
                    .Where(request => request.UserId.Equals(userId)
                        && request.Status == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Completed
                        && request.NotificationCount < 2)
                    .OrderByDescending(request => request.RequestedAt)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return rows.Select(row => ToContractModel(row)).ToList();
            }
        }

        /// <inheritdoc />
        public async Task BulkMarkNotificationViewed(Guid userId, IReadOnlyCollection<Guid> requestIds)
        {
            if (userId.IsEmpty())
            {
                throw new ArgumentException("User id cannot be empty.", nameof(userId));
            }

            if (requestIds.Count == 0)
            {
                return;
            }

            var uniqueRequestIds = requestIds
                .Where(requestId => !requestId.IsEmpty())
                .Distinct()
                .ToArray();

            if (uniqueRequestIds.Length == 0)
            {
                return;
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var rows = await dbContext.ContentRequests
                    .Where(request => request.UserId.Equals(userId)
                        && request.Status == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Completed
                        && uniqueRequestIds.Contains(request.Id))
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (rows.Count == 0)
                {
                    return;
                }

                foreach (var row in rows)
                {
                    if (row.NotificationCount < int.MaxValue)
                    {
                        row.NotificationCount++;
                    }
                }

                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        private static void ValidateType(ContentRequestType type)
        {
            if (type != ContentRequestType.Movie && type != ContentRequestType.Series)
            {
                throw new ArgumentException("Invalid request type.", nameof(type));
            }
        }

        private static int? NormalizeSeasonNumber(ContentRequestType type, int? seasonNumber)
        {
            if (type == ContentRequestType.Series)
            {
                if (!seasonNumber.HasValue || seasonNumber.Value <= 0)
                {
                    throw new ArgumentException("Season number is required for series requests and must be greater than zero.", nameof(seasonNumber));
                }

                return seasonNumber.Value;
            }

            return null;
        }

        private static string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var trimmedTitle = title.Trim();
            var collapsedTitle = _whitespaceRegex.Replace(trimmedTitle, " ");
            return collapsedTitle.ToLowerInvariant();
        }

        private static void EnsureTransitionAllowed(
            Jellyfin.Database.Implementations.Enums.ContentRequestStatus currentStatus,
            Jellyfin.Database.Implementations.Enums.ContentRequestStatus nextStatus)
        {
            if (currentStatus == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Pending
                && (nextStatus == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Approved
                    || nextStatus == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Rejected))
            {
                return;
            }

            if (currentStatus == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Approved
                && (nextStatus == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Completed
                    || nextStatus == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Rejected))
            {
                return;
            }

            throw new ContentRequestConflictException(
                $"Invalid state transition from '{currentStatus}' to '{nextStatus}'.");
        }

        private static Jellyfin.Database.Implementations.Enums.ContentRequestType ToDatabaseType(ContentRequestType type)
            => type switch
            {
                ContentRequestType.Movie => Jellyfin.Database.Implementations.Enums.ContentRequestType.Movie,
                ContentRequestType.Series => Jellyfin.Database.Implementations.Enums.ContentRequestType.Series,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };

        private static ContentRequestType ToContractType(Jellyfin.Database.Implementations.Enums.ContentRequestType type)
            => type switch
            {
                Jellyfin.Database.Implementations.Enums.ContentRequestType.Movie => ContentRequestType.Movie,
                Jellyfin.Database.Implementations.Enums.ContentRequestType.Series => ContentRequestType.Series,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };

        private static ContentRequestStatus ToContractStatus(Jellyfin.Database.Implementations.Enums.ContentRequestStatus status)
            => status switch
            {
                Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Pending => ContentRequestStatus.Pending,
                Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Approved => ContentRequestStatus.Approved,
                Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Rejected => ContentRequestStatus.Rejected,
                Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Completed => ContentRequestStatus.Completed,
                _ => throw new ArgumentOutOfRangeException(nameof(status))
            };

        private static ContentRequestInfo ToContractModel(ContentRequest row, string? username = null)
            => new()
            {
                Id = row.Id,
                UserId = row.UserId,
                Username = username,
                Title = row.Title,
                NormalizedTitle = row.NormalizedTitle,
                Type = ToContractType(row.Type),
                SeasonNumber = row.SeasonNumber,
                RequestedAt = row.RequestedAt,
                Status = ToContractStatus(row.Status),
                JellyfinItemId = row.JellyfinItemId,
                NotificationCount = row.NotificationCount,
                IsAdminViewed = row.IsAdminViewed
            };

        private static async Task<(int UsedMovies, int UsedSeries)> GetCycleUsageAsync(
            JellyfinDbContext dbContext,
            Guid userId,
            DateTime cycleStartDate)
        {
            var groupedUsage = await dbContext.ContentRequests
                .AsNoTracking()
                .Where(request => request.UserId.Equals(userId)
                    && request.RequestedAt >= cycleStartDate
                    && (request.Status == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Pending
                        || request.Status == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Approved
                        || request.Status == Jellyfin.Database.Implementations.Enums.ContentRequestStatus.Completed))
                .GroupBy(request => request.Type)
                .Select(group => new
                {
                    group.Key,
                    Count = group.Count()
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var usedMovies = groupedUsage
                .Where(row => row.Key == Jellyfin.Database.Implementations.Enums.ContentRequestType.Movie)
                .Select(row => row.Count)
                .FirstOrDefault();

            var usedSeries = groupedUsage
                .Where(row => row.Key == Jellyfin.Database.Implementations.Enums.ContentRequestType.Series)
                .Select(row => row.Count)
                .FirstOrDefault();

            return (usedMovies, usedSeries);
        }

        private static DateTime GetCurrentCycleStart(DateTime subscriptionStartDateUtc, DateTime nowUtc)
        {
            var fullMonthsElapsed = ((nowUtc.Year - subscriptionStartDateUtc.Year) * 12) + nowUtc.Month - subscriptionStartDateUtc.Month;
            var candidateCycleStart = subscriptionStartDateUtc.AddMonths(fullMonthsElapsed);

            if (candidateCycleStart > nowUtc)
            {
                candidateCycleStart = subscriptionStartDateUtc.AddMonths(fullMonthsElapsed - 1);
            }

            while (candidateCycleStart.AddMonths(1) <= nowUtc)
            {
                candidateCycleStart = candidateCycleStart.AddMonths(1);
            }

            return candidateCycleStart;
        }

        private static async Task<SubscriptionCycleInfo> ResolveSubscriptionCycleAsync(
            JellyfinDbContext dbContext,
            Guid userId,
            DateTime nowUtc)
        {
            var user = await dbContext.Users
                .AsNoTracking()
                .Where(dbUser => dbUser.Id.Equals(userId))
                .Select(dbUser => new
                {
                    dbUser.Id,
                    dbUser.ExpiryDate
                })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (user is null)
            {
                throw new ContentRequestInactiveSubscriptionException("User not found.");
            }

            var redeemedKeys = await dbContext.AccessKeys
                .AsNoTracking()
                .Where(accessKey => accessKey.IsRedeemed
                    && accessKey.RedeemedByUserId.HasValue
                    && accessKey.RedeemedByUserId.Value.Equals(userId)
                    && accessKey.RedeemedAt.HasValue)
                .OrderBy(accessKey => accessKey.RedeemedAt)
                .Select(accessKey => new
                {
                    RedeemedAt = accessKey.RedeemedAt!.Value,
                    accessKey.DurationMonths
                })
                .ToListAsync()
                .ConfigureAwait(false);

            if (redeemedKeys.Count == 0
                && user.ExpiryDate.HasValue
                && user.ExpiryDate.Value > nowUtc)
            {
                // Some legacy users can be active via ExpiryDate without a redeemed access key record.
                // Keep quota deterministic by counting against the full active history for that user.
                return new SubscriptionCycleInfo(true, DateTime.UnixEpoch);
            }

            if (redeemedKeys.Count == 0)
            {
                return new SubscriptionCycleInfo(false, nowUtc);
            }

            // Build the active streak anchor from redeemed keys. A redemption after a lapse
            // starts a fresh cycle anchor, while in-time renewals extend the same streak.
            var currentStreakStart = redeemedKeys[0].RedeemedAt;
            var currentStreakExpiry = currentStreakStart.AddMonths(redeemedKeys[0].DurationMonths);
            for (var index = 1; index < redeemedKeys.Count; index++)
            {
                var redeemedKey = redeemedKeys[index];
                if (redeemedKey.RedeemedAt <= currentStreakExpiry)
                {
                    currentStreakExpiry = currentStreakExpiry.AddMonths(redeemedKey.DurationMonths);
                    continue;
                }

                currentStreakStart = redeemedKey.RedeemedAt;
                currentStreakExpiry = redeemedKey.RedeemedAt.AddMonths(redeemedKey.DurationMonths);
            }

            if (!user.ExpiryDate.HasValue || user.ExpiryDate.Value <= nowUtc)
            {
                return new SubscriptionCycleInfo(false, nowUtc);
            }

            if (nowUtc < currentStreakStart)
            {
                return new SubscriptionCycleInfo(false, currentStreakStart);
            }

            return new SubscriptionCycleInfo(
                true,
                GetCurrentCycleStart(currentStreakStart, nowUtc));
        }

        private sealed record SubscriptionCycleInfo(bool IsSubscriptionActive, DateTime CycleStartDate);
    }
}
