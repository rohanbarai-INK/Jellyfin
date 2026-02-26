using System;

namespace MediaBrowser.Controller.ContentRequests
{
    /// <summary>
    /// Thrown when request row is not found.
    /// </summary>
    public sealed class ContentRequestNotFoundException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContentRequestNotFoundException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        public ContentRequestNotFoundException(string message)
            : base(message)
        {
        }
    }
}
