using System;
using System.Collections.Generic;

namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Trending Now response payload.
    /// </summary>
    public sealed class TrendingNowResponseDto
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
        /// Gets or sets the UTC period start.
        /// </summary>
        public DateTime PeriodStartUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC period end.
        /// </summary>
        public DateTime PeriodEndUtc { get; set; }

        /// <summary>
        /// Gets or sets the number of rows returned.
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Gets or sets the number of candidates considered.
        /// </summary>
        public int CandidateCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether fallback behavior was used.
        /// </summary>
        public bool UsedFallbackMode { get; set; }

        /// <summary>
        /// Gets or sets trending rows.
        /// </summary>
        public IReadOnlyList<TrendingNowItemDto> Items { get; set; } = [];
    }
}
