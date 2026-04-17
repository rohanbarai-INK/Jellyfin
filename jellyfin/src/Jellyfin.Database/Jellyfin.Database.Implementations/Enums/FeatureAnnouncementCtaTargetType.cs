namespace Jellyfin.Database.Implementations.Enums
{
    /// <summary>
    /// Database CTA target type for feature announcements.
    /// </summary>
    public enum FeatureAnnouncementCtaTargetType
    {
        /// <summary>
        /// Internal route target.
        /// </summary>
        InternalRoute = 0,

        /// <summary>
        /// External URL target.
        /// </summary>
        ExternalUrl = 1
    }
}
