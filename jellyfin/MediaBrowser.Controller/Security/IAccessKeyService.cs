using System;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Security
{
    /// <summary>
    /// Service for creating and redeeming user access keys.
    /// </summary>
    public interface IAccessKeyService
    {
        /// <summary>
        /// Generates a new access key.
        /// </summary>
        /// <param name="months">The duration in months the key should grant.</param>
        /// <returns>A <see cref="GeneratedAccessKeyResult"/>.</returns>
        Task<GeneratedAccessKeyResult> GenerateKey(int months);

        /// <summary>
        /// Redeems an existing access key for the provided user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="keyString">The key to redeem.</param>
        /// <returns>A <see cref="RedeemedAccessKeyResult"/>.</returns>
        Task<RedeemedAccessKeyResult> RedeemKey(Guid userId, string keyString);

        /// <summary>
        /// Gets subscription metadata for the provided user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns>A <see cref="CurrentSubscriptionResult"/>.</returns>
        Task<CurrentSubscriptionResult> GetCurrentSubscription(Guid userId);

        /// <summary>
        /// Gets a value indicating whether the user is inside the configured grace period.
        /// </summary>
        /// <param name="expiryDate">The user expiry date in UTC.</param>
        /// <returns><c>true</c> if within grace period; otherwise <c>false</c>.</returns>
        bool IsWithinGracePeriod(DateTime? expiryDate);

        /// <summary>
        /// Gets the remaining grace days for the supplied expiry date.
        /// </summary>
        /// <param name="expiryDate">The user expiry date in UTC.</param>
        /// <returns>The remaining grace days, or 0 when grace does not apply.</returns>
        int GetGraceDaysRemaining(DateTime? expiryDate);
    }
}
