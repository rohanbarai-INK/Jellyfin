using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Trending
{
    /// <summary>
    /// Admin promotion service for Trending Now.
    /// </summary>
    public interface ITrendingPromotionService
    {
        /// <summary>
        /// Gets currently active promotions for runtime ranking.
        /// </summary>
        /// <param name="nowUtc">Current UTC timestamp.</param>
        /// <returns>Eligible promotion list before audience matching.</returns>
        Task<IReadOnlyList<TrendingPromotionInfo>> GetActivePromotions(DateTime nowUtc);

        /// <summary>
        /// Gets admin promotions for dashboard management.
        /// </summary>
        /// <returns>Promotion list.</returns>
        Task<IReadOnlyList<TrendingPromotionInfo>> GetAdminPromotions();

        /// <summary>
        /// Creates or updates a promotion.
        /// </summary>
        /// <param name="options">Promotion payload.</param>
        /// <param name="actorUserId">Authenticated admin user.</param>
        /// <returns>The saved promotion.</returns>
        Task<TrendingPromotionInfo> UpsertPromotion(TrendingPromotionUpsertInfo options, Guid actorUserId);

        /// <summary>
        /// Sets promotion enabled state.
        /// </summary>
        /// <param name="promotionId">Promotion database identifier.</param>
        /// <param name="enabled">New enabled state.</param>
        /// <param name="actorUserId">Authenticated admin user.</param>
        /// <returns>The updated promotion.</returns>
        Task<TrendingPromotionInfo> SetEnabled(Guid promotionId, bool enabled, Guid actorUserId);

        /// <summary>
        /// Deletes a promotion.
        /// </summary>
        /// <param name="promotionId">Promotion database identifier.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task DeletePromotion(Guid promotionId);
    }
}
