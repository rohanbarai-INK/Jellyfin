namespace MediaBrowser.Controller.FeatureAnnouncements
{
    /// <summary>
    /// Target type for announcement CTA.
    /// </summary>
    public enum FeatureAnnouncementCtaTargetType
    {
        /// <summary>
        /// Navigate to an internal app route.
        /// </summary>
        InternalRoute = 0,

        /// <summary>
        /// Navigate to an external URL.
        /// </summary>
        ExternalUrl = 1
    }
}
