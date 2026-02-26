using System;

namespace MediaBrowser.Controller.ContentRequests
{
    /// <summary>
    /// Thrown when request operation conflicts with workflow, cap, or duplicate rules.
    /// </summary>
    public sealed class ContentRequestConflictException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContentRequestConflictException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        public ContentRequestConflictException(string message)
            : base(message)
        {
        }
    }
}
