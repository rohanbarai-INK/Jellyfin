namespace MediaBrowser.Controller.FeatureAnnouncements
{
    /// <summary>
    /// Announcement publish status.
    /// </summary>
    public enum FeatureAnnouncementStatus
    {
        /// <summary>
        /// Draft announcement that should not be shown to end users.
        /// </summary>
        Draft = 0,

        /// <summary>
        /// Published announcement eligible for end-user visibility.
        /// </summary>
        Published = 1
    }
}
