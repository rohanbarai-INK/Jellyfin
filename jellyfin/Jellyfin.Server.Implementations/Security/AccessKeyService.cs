using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Extensions;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Security;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Server.Implementations.Security
{
    /// <inheritdoc />
    public class AccessKeyService : IAccessKeyService
    {
        private static readonly HashSet<int> _allowedDurations = new() { 1, 3, 6, 12 };
        private static readonly char[] _alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
        private readonly IUserManager _userManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessKeyService"/> class.
        /// </summary>
        /// <param name="dbProvider">The database provider.</param>
        /// <param name="userManager">The user manager.</param>
        public AccessKeyService(
            IDbContextFactory<JellyfinDbContext> dbProvider,
            IUserManager userManager)
        {
            _dbProvider = dbProvider;
            _userManager = userManager;
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
                var effectiveStart = dbUser.ExpiryDate.HasValue && dbUser.ExpiryDate.Value > now
                    ? dbUser.ExpiryDate.Value
                    : now;
                var updatedExpiryDate = effectiveStart.AddMonths(accessKey.DurationMonths);

                dbUser.ExpiryDate = updatedExpiryDate;

                accessKey.IsRedeemed = true;
                accessKey.RedeemedByUserId = userId;
                accessKey.RedeemedAt = now;

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

                return new CurrentSubscriptionResult(
                    user.ExpiryDate,
                    user.Status.ToString(),
                    latestRedeemedKey?.DurationMonths,
                    latestRedeemedKey?.RedeemedAt);
            }
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
    }
}
