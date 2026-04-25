namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Enabled-state request for a Trending promotion.
    /// </summary>
    public sealed class SetTrendingPromotionEnabledRequest
    {
        /// <summary>
        /// Gets or sets a value indicating whether the promotion should be enabled.
        /// </summary>
        public bool Enabled { get; set; }
    }
}
