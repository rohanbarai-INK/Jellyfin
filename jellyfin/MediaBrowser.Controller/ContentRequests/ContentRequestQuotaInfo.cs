using System;

namespace MediaBrowser.Controller.ContentRequests
{
    /// <summary>
    /// Quota summary for request caps.
    /// </summary>
    public sealed class ContentRequestQuotaInfo
    {
        /// <summary>
        /// Gets or sets the cycle start date in UTC.
        /// </summary>
        public DateTime CycleStartDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether subscription is active for creating requests.
        /// </summary>
        public bool IsSubscriptionActive { get; set; }

        /// <summary>
        /// Gets or sets movie cap.
        /// </summary>
        public int MovieCap { get; set; }

        /// <summary>
        /// Gets or sets series cap.
        /// </summary>
        public int SeriesCap { get; set; }

        /// <summary>
        /// Gets or sets used movie slots in current cycle.
        /// </summary>
        public int UsedMovies { get; set; }

        /// <summary>
        /// Gets or sets used series slots in current cycle.
        /// </summary>
        public int UsedSeries { get; set; }

        /// <summary>
        /// Gets or sets remaining movie slots.
        /// </summary>
        public int RemainingMovies { get; set; }

        /// <summary>
        /// Gets or sets remaining series slots.
        /// </summary>
        public int RemainingSeries { get; set; }
    }
}
