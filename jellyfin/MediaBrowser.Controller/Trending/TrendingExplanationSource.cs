namespace MediaBrowser.Controller.Trending
{
    /// <summary>
    /// Explains which layer most strongly influenced the surfaced title.
    /// </summary>
    public enum TrendingExplanationSource
    {
        /// <summary>
        /// The item is present mainly because of global trending momentum.
        /// </summary>
        BaseTrending = 0,

        /// <summary>
        /// The item was re-ranked due to viewer affinity.
        /// </summary>
        Personalization = 1,

        /// <summary>
        /// The item was editorially promoted.
        /// </summary>
        AdminPromotion = 2
    }
}
