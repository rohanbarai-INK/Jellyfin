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
        /// <param name="lastDurationMonths">The most recently redeemed duration in months.</param>
        /// <param name="lastRedeemedAt">The most recent key redemption time in UTC.</param>
        public CurrentSubscriptionResult(DateTime? expiryDate, string status, int? lastDurationMonths, DateTime? lastRedeemedAt)
        {
            ExpiryDate = expiryDate;
            Status = status;
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
        /// Gets the most recently redeemed duration in months.
        /// </summary>
        public int? LastDurationMonths { get; }

        /// <summary>
        /// Gets the most recent key redemption timestamp in UTC.
        /// </summary>
        public DateTime? LastRedeemedAt { get; }
    }
}
