namespace Jellyfin.Database.Implementations.Enums
{
    /// <summary>
    /// Request workflow status.
    /// </summary>
    public enum ContentRequestStatus
    {
        /// <summary>
        /// Waiting for admin action.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Approved by admin, awaiting completion.
        /// </summary>
        Approved = 1,

        /// <summary>
        /// Rejected by admin.
        /// </summary>
        Rejected = 2,

        /// <summary>
        /// Completed and linked to a Jellyfin item.
        /// </summary>
        Completed = 3
    }
}
