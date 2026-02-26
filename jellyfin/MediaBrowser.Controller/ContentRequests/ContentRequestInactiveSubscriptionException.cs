using System;

namespace MediaBrowser.Controller.ContentRequests
{
    /// <summary>
    /// Thrown when user subscription is inactive for request creation.
    /// </summary>
    public sealed class ContentRequestInactiveSubscriptionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContentRequestInactiveSubscriptionException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        public ContentRequestInactiveSubscriptionException(string message)
            : base(message)
        {
        }
    }
}
