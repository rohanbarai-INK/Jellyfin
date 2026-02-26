using System.Collections.Generic;

namespace MediaBrowser.Controller.ContentRequests
{
    /// <summary>
    /// Paged content request list result.
    /// </summary>
    public sealed class ContentRequestListResult
    {
        /// <summary>
        /// Gets or sets rows.
        /// </summary>
        public IReadOnlyList<ContentRequestInfo> Items { get; set; } = [];

        /// <summary>
        /// Gets or sets total records.
        /// </summary>
        public int TotalRecordCount { get; set; }
    }
}
