using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Extensions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Security;
using MediaBrowser.Model.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Server.Implementations.Security
{
    /// <inheritdoc />
    public class AccessKeyService : IAccessKeyService
    {
        private const string SubscriptionConfigKey = "subscription";

        private static readonly HashSet<int> _allowedDurations = new() { 1, 3, 6, 12 };
        private static readonly char[] _alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
        private readonly IUserManager _userManager;
        private readonly IServerConfigurationManager _configurationManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessKeyService"/> class.
        /// </summary>
        /// <param name="dbProvider">The database provider.</param>
        /// <param name="userManager">The user manager.</param>
        /// <param name="configurationManager">The server configuration manager.</param>
        public AccessKeyService(
            IDbContextFactory<JellyfinDbContext> dbProvider,
            IUserManager userManager,
            IServerConfigurationManager configurationManager)
        {
            _dbProvider = dbProvider;
            _userManager = userManager;
            _configurationManager = configurationManager;
        }

        /// <inheritdoc />
        public async Task<GeneratedAccessKeyResult> GenerateKey(int months)
        {
            ValidateDuration(months);

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    var keyValue = CreateFormattedKey();
                    var alreadyExists = await dbContext.AccessKeys
                        .AnyAsync(accessKey => accessKey.Key == keyValue)
                        .ConfigureAwait(false);

                    if (alreadyExists)
                    {
                        continue;
                    }

                    var accessKey = new AccessKey(keyValue, months);
                    dbContext.AccessKeys.Add(accessKey);
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);

                    return new GeneratedAccessKeyResult(accessKey.Key, accessKey.DurationMonths, accessKey.CreatedAt);
                }
            }

            throw new InvalidOperationException("Unable to generate a unique access key.");
        }

        /// <inheritdoc />
        public async Task<RedeemedAccessKeyResult> RedeemKey(Guid userId, string keyString)
        {
            if (userId.IsEmpty())
            {
                throw new ArgumentException("User id cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(keyString))
            {
                throw new ArgumentException("Key cannot be empty.", nameof(keyString));
            }

            var normalizedKey = keyString.Trim().ToUpperInvariant();
            var user = _userManager.GetUserById(userId) ?? throw new ResourceNotFoundException("User not found.");

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var accessKey = await dbContext.AccessKeys
                    .FirstOrDefaultAsync(dbAccessKey => dbAccessKey.Key == normalizedKey)
                    .ConfigureAwait(false);

                if (accessKey is null)
                {
                    throw new AccessKeyNotFoundException("Access key not found.");
                }

                if (accessKey.IsRedeemed)
                {
                    throw new AccessKeyAlreadyRedeemedException("Access key has already been redeemed.");
                }

                var dbUser = await dbContext.Users.FirstOrDefaultAsync(dbUser => dbUser.Id.Equals(userId)).ConfigureAwait(false)
                    ?? throw new ResourceNotFoundException("User not found.");

                var now = DateTime.UtcNow;
                var renewalStartDate = now;
                if (dbUser.ExpiryDate.HasValue && dbUser.ExpiryDate.Value > now)
                {
                    // Active users keep their remaining days; renewal extends from current expiry.
                    renewalStartDate = dbUser.ExpiryDate.Value;
                }

                var updatedExpiryDate = CalculateUpdatedExpiryDate(renewalStartDate, accessKey.DurationMonths);
                var redeemedAmount = GetPlanPriceForDuration(accessKey.DurationMonths);

                dbUser.ExpiryDate = updatedExpiryDate;

                accessKey.IsRedeemed = true;
                accessKey.RedeemedByUserId = userId;
                accessKey.RedeemedAt = now;
                accessKey.RedeemedAmount = redeemedAmount;
                accessKey.CycleStartDate = renewalStartDate;
                accessKey.CycleEndDate = updatedExpiryDate;

                await dbContext.SaveChangesAsync().ConfigureAwait(false);

                // Keep the in-memory user cache in sync.
                user.ExpiryDate = updatedExpiryDate;

                return new RedeemedAccessKeyResult(updatedExpiryDate, accessKey.DurationMonths, now);
            }
        }

        /// <inheritdoc />
        public async Task<CurrentSubscriptionResult> GetCurrentSubscription(Guid userId)
        {
            if (userId.IsEmpty())
            {
                throw new ArgumentException("User id cannot be empty.", nameof(userId));
            }

            var user = _userManager.GetUserById(userId) ?? throw new ResourceNotFoundException("User not found.");
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var latestRedeemedKey = await dbContext.AccessKeys
                    .Where(accessKey => accessKey.IsRedeemed
                        && accessKey.RedeemedByUserId.HasValue
                        && accessKey.RedeemedByUserId.Value.Equals(userId))
                    .OrderByDescending(accessKey => accessKey.RedeemedAt)
                    .ThenByDescending(accessKey => accessKey.CreatedAt)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                var isInGracePeriod = IsWithinGracePeriod(user.ExpiryDate);
                var graceDaysRemaining = GetGraceDaysRemaining(user.ExpiryDate);
                var status = isInGracePeriod ? "Grace" : user.Status.ToString();

                return new CurrentSubscriptionResult(
                    user.ExpiryDate,
                    status,
                    isInGracePeriod,
                    graceDaysRemaining,
                    latestRedeemedKey?.DurationMonths,
                    latestRedeemedKey?.RedeemedAt);
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<BillingHistoryEntryResult>> GetBillingHistory(Guid userId)
        {
            if (userId.IsEmpty())
            {
                throw new ArgumentException("User id cannot be empty.", nameof(userId));
            }

            var now = DateTime.UtcNow;
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var redeemedKeys = await dbContext.AccessKeys
                    .Where(accessKey => accessKey.IsRedeemed
                        && accessKey.RedeemedByUserId.HasValue
                        && accessKey.RedeemedByUserId.Value.Equals(userId))
                    .OrderBy(accessKey => accessKey.RedeemedAt ?? accessKey.CreatedAt)
                    .ThenBy(accessKey => accessKey.CreatedAt)
                    .ToListAsync()
                    .ConfigureAwait(false);

                var items = new List<BillingHistoryEntryResult>(redeemedKeys.Count);
                DateTime? previousCycleEndDate = null;
                foreach (var accessKey in redeemedKeys)
                {
                    var redeemedAt = accessKey.RedeemedAt ?? accessKey.CreatedAt;
                    var cycleStartDate = accessKey.CycleStartDate ?? redeemedAt;
                    if (!accessKey.CycleStartDate.HasValue
                        && previousCycleEndDate.HasValue
                        && previousCycleEndDate.Value > cycleStartDate)
                    {
                        cycleStartDate = previousCycleEndDate.Value;
                    }

                    var cycleEndDate = accessKey.CycleEndDate ?? CalculateUpdatedExpiryDate(cycleStartDate, accessKey.DurationMonths);
                    previousCycleEndDate = cycleEndDate;
                    var status = cycleEndDate >= now ? "Active" : "Expired";
                    var amount = accessKey.RedeemedAmount ?? GetPlanPriceForDuration(accessKey.DurationMonths);

                    items.Add(new BillingHistoryEntryResult(
                        accessKey.Key,
                        accessKey.DurationMonths,
                        cycleStartDate,
                        cycleEndDate,
                        redeemedAt,
                        amount,
                        status));
                }

                items.Reverse();
                return items;
            }
        }

        /// <inheritdoc />
        public bool IsWithinGracePeriod(DateTime? expiryDate)
        {
            if (!expiryDate.HasValue)
            {
                return false;
            }

            var now = DateTime.UtcNow;
            if (expiryDate.Value > now)
            {
                return false;
            }

            var graceDays = GetGracePeriodDays();
            if (graceDays <= 0)
            {
                return false;
            }

            var graceEnd = expiryDate.Value.AddDays(graceDays);
            return now <= graceEnd;
        }

        /// <inheritdoc />
        public int GetGraceDaysRemaining(DateTime? expiryDate)
        {
            if (!IsWithinGracePeriod(expiryDate))
            {
                return 0;
            }

            var graceEnd = expiryDate!.Value.AddDays(GetGracePeriodDays());
            var remaining = graceEnd - DateTime.UtcNow;
            return Math.Max(0, (int)Math.Ceiling(remaining.TotalDays));
        }

        private static string CreateFormattedKey()
        {
            var buffer = new char[8];
            Span<byte> randomBytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(randomBytes);

            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = _alphabet[randomBytes[i] % _alphabet.Length];
            }

            return string.Concat(
                "JF-",
                new string(buffer, 0, 4),
                "-",
                new string(buffer, 4, 4));
        }

        private static void ValidateDuration(int months)
        {
            if (_allowedDurations.Contains(months))
            {
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(months), "Duration must be one of: 1, 3, 6, 12.");
        }

        private static DateTime CalculateUpdatedExpiryDate(DateTime redeemedAtUtc, int durationMonths)
        {
            var monthBasedExpiry = redeemedAtUtc.AddMonths(durationMonths);
            if (redeemedAtUtc.Month != 2)
            {
                return monthBasedExpiry;
            }

            // February renewals must provide at least 30 days per purchased month.
            var minimumFairExpiry = redeemedAtUtc.AddDays(durationMonths * 30);
            return monthBasedExpiry >= minimumFairExpiry ? monthBasedExpiry : minimumFairExpiry;
        }

        private int GetGracePeriodDays()
        {
            var configuration = _configurationManager.GetConfiguration<SubscriptionConfiguration>(SubscriptionConfigKey);
            return Math.Max(0, configuration.GracePeriodDays);
        }

        private decimal GetPlanPriceForDuration(int durationMonths)
        {
            var configuration = _configurationManager.GetConfiguration<SubscriptionConfiguration>(SubscriptionConfigKey);
            return durationMonths switch
            {
                1 => configuration.OneMonthPrice,
                3 => configuration.ThreeMonthPrice,
                6 => configuration.SixMonthPrice,
                12 => configuration.TwelveMonthPrice,
                _ => configuration.BasePricePerMonth * durationMonths
            };
        }
    }
}
