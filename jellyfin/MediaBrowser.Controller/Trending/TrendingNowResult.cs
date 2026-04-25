using System;
using System.Collections.Generic;

namespace MediaBrowser.Controller.Trending
{
    /// <summary>
    /// Aggregated Trending Now payload.
    /// </summary>
    public sealed class TrendingNowResult
    {
        /// <summary>
        /// Gets or sets the requested period key.
        /// </summary>
        public string PeriodKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the requested period label.
        /// </summary>
        public string PeriodLabel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC start for the requested period.
        /// </summary>
        public DateTime PeriodStartUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC end for the requested period.
        /// </summary>
        public DateTime PeriodEndUtc { get; set; }

        /// <summary>
        /// Gets or sets the number of rows returned.
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Gets or sets the number of candidates considered before ranking layers were applied.
        /// </summary>
        public int CandidateCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the result used fallback behavior due to low data.
        /// </summary>
        public bool UsedFallbackMode { get; set; }

        /// <summary>
        /// Gets or sets the discovery rows.
        /// </summary>
        public IReadOnlyList<TrendingNowItemResult> Items { get; set; } = [];
    }
}
