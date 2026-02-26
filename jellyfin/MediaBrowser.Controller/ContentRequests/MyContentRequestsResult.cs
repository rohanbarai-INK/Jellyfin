using System.Collections.Generic;

namespace MediaBrowser.Controller.ContentRequests
{
    /// <summary>
    /// Current user content requests and quota summary.
    /// </summary>
    public sealed class MyContentRequestsResult
    {
        /// <summary>
        /// Gets or sets request rows.
        /// </summary>
        public IReadOnlyList<ContentRequestInfo> Requests { get; set; } = [];

        /// <summary>
        /// Gets or sets quota summary.
        /// </summary>
        public ContentRequestQuotaInfo Quota { get; set; } = new();
    }
}
