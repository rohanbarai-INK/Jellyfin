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
    }
}
