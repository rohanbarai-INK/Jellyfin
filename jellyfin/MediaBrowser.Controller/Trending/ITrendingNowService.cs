using System;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Trending
{
    /// <summary>
    /// Provides Trending Now discovery aggregates.
    /// </summary>
    public interface ITrendingNowService
    {
        /// <summary>
        /// Gets the current trending titles for the requested period.
        /// </summary>
        /// <param name="requestingUserId">The authenticated user requesting the data.</param>
        /// <param name="periodType">The requested trending period.</param>
        /// <param name="limit">Maximum rows to return.</param>
        /// <returns>A discovery payload for the requested period.</returns>
        Task<TrendingNowResult> GetTrendingNow(Guid requestingUserId, TrendingNowPeriodType periodType, int limit);
    }
}
