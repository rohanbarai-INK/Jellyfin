using System;

namespace MediaBrowser.Controller.Security
{
    /// <summary>
    /// A single immutable billing history record for a redeemed subscription key.
    /// </summary>
    public sealed class BillingHistoryEntryResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BillingHistoryEntryResult"/> class.
        /// </summary>
        /// <param name="reference">The billing reference identifier.</param>
        /// <param name="durationMonths">The redeemed duration in months.</param>
        /// <param name="cycleStartDate">The billing cycle start date in UTC.</param>
        /// <param name="cycleEndDate">The billing cycle end date in UTC.</param>
        /// <param name="redeemedAt">The redeem date in UTC.</param>
        /// <param name="amount">The immutable redeemed amount.</param>
        /// <param name="status">The cycle status.</param>
        public BillingHistoryEntryResult(
            string reference,
            int durationMonths,
            DateTime cycleStartDate,
            DateTime cycleEndDate,
            DateTime redeemedAt,
            decimal amount,
            string status)
        {
            Reference = reference;
            DurationMonths = durationMonths;
            CycleStartDate = cycleStartDate;
            CycleEndDate = cycleEndDate;
            RedeemedAt = redeemedAt;
            Amount = amount;
            Status = status;
        }

        /// <summary>
        /// Gets the billing reference identifier.
        /// </summary>
        public string Reference { get; }

        /// <summary>
        /// Gets the redeemed duration in months.
        /// </summary>
        public int DurationMonths { get; }

        /// <summary>
        /// Gets the billing cycle start date in UTC.
        /// </summary>
        public DateTime CycleStartDate { get; }

        /// <summary>
        /// Gets the billing cycle end date in UTC.
        /// </summary>
        public DateTime CycleEndDate { get; }

        /// <summary>
        /// Gets the redemption timestamp in UTC.
        /// </summary>
        public DateTime RedeemedAt { get; }

        /// <summary>
        /// Gets the immutable billed amount.
        /// </summary>
        public decimal Amount { get; }

        /// <summary>
        /// Gets the billing status.
        /// </summary>
        public string Status { get; }
    }
}
