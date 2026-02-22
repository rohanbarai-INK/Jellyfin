using System;

namespace MediaBrowser.Controller.Security
{
    /// <summary>
    /// Result returned when reading current user subscription metadata.
    /// </summary>
    public sealed class CurrentSubscriptionResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CurrentSubscriptionResult"/> class.
        /// </summary>
        /// <param name="expiryDate">The user's expiry date in UTC.</param>
        /// <param name="status">The current status.</param>
        /// <param name="isInGracePeriod">Whether the user is currently in grace period.</param>
        /// <param name="graceDaysRemaining">Remaining grace days.</param>
        /// <param name="lastDurationMonths">The most recently redeemed duration in months.</param>
        /// <param name="lastRedeemedAt">The most recent key redemption time in UTC.</param>
        public CurrentSubscriptionResult(
            DateTime? expiryDate,
            string status,
            bool isInGracePeriod,
            int graceDaysRemaining,
            int? lastDurationMonths,
            DateTime? lastRedeemedAt)
        {
            ExpiryDate = expiryDate;
            Status = status;
            IsInGracePeriod = isInGracePeriod;
            GraceDaysRemaining = graceDaysRemaining;
            LastDurationMonths = lastDurationMonths;
            LastRedeemedAt = lastRedeemedAt;
        }

        /// <summary>
        /// Gets the user's current expiry date.
        /// </summary>
        public DateTime? ExpiryDate { get; }

        /// <summary>
        /// Gets the user's current status.
        /// </summary>
        public string Status { get; }

        /// <summary>
        /// Gets a value indicating whether the user is currently in grace period.
        /// </summary>
        public bool IsInGracePeriod { get; }

        /// <summary>
        /// Gets remaining grace days.
        /// </summary>
        public int GraceDaysRemaining { get; }

        /// <summary>
        /// Gets the most recently redeemed duration in months.
        /// </summary>
        public int? LastDurationMonths { get; }

        /// <summary>
        /// Gets the most recent key redemption timestamp in UTC.
        /// </summary>
        public DateTime? LastRedeemedAt { get; }
    }
}
