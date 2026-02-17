using System;

namespace MediaBrowser.Controller.Security
{
    /// <summary>
    /// Result returned when redeeming an access key.
    /// </summary>
    public sealed class RedeemedAccessKeyResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RedeemedAccessKeyResult"/> class.
        /// </summary>
        /// <param name="expiryDate">The user's new expiry date in UTC.</param>
        /// <param name="durationMonths">The key duration in months.</param>
        /// <param name="redeemedAt">The redemption timestamp in UTC.</param>
        public RedeemedAccessKeyResult(DateTime? expiryDate, int durationMonths, DateTime redeemedAt)
        {
            ExpiryDate = expiryDate;
            DurationMonths = durationMonths;
            RedeemedAt = redeemedAt;
        }

        /// <summary>
        /// Gets the user's updated expiry date.
        /// </summary>
        public DateTime? ExpiryDate { get; }

        /// <summary>
        /// Gets the key duration in months.
        /// </summary>
        public int DurationMonths { get; }

        /// <summary>
        /// Gets the redemption timestamp in UTC.
        /// </summary>
        public DateTime RedeemedAt { get; }
    }
}
