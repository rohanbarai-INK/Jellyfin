using System;

namespace MediaBrowser.Controller.ContentRequests
{
    /// <summary>
    /// Admin view of a user's request quota state.
    /// </summary>
    public sealed class ContentRequestAdminUserQuotaResult
    {
        /// <summary>
        /// Gets or sets user id.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets quota summary.
        /// </summary>
        public ContentRequestQuotaInfo Quota { get; set; } = new();
    }
}
