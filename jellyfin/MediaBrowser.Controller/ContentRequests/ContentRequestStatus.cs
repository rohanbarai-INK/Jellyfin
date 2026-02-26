namespace MediaBrowser.Controller.ContentRequests
{
    /// <summary>
    /// Content request workflow status.
    /// </summary>
    public enum ContentRequestStatus
    {
        /// <summary>
        /// Request is waiting for admin action.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Request approved by admin.
        /// </summary>
        Approved = 1,

        /// <summary>
        /// Request rejected by admin.
        /// </summary>
        Rejected = 2,

        /// <summary>
        /// Request fulfilled and linked to a Jellyfin item.
        /// </summary>
        Completed = 3
    }
}
