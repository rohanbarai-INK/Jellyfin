using System;

namespace MediaBrowser.Controller.ContentRequests
{
    /// <summary>
    /// Lightweight admin user suggestion for request reward assignment.
    /// </summary>
    public sealed class ContentRequestUserSuggestion
    {
        /// <summary>
        /// Gets or sets user id.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets username.
        /// </summary>
        public string Username { get; set; } = string.Empty;
    }
}
