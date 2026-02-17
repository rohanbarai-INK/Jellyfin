using System;

namespace MediaBrowser.Controller.Security
{
    /// <summary>
    /// Exception thrown when the provided access key does not exist.
    /// </summary>
    public sealed class AccessKeyNotFoundException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AccessKeyNotFoundException"/> class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public AccessKeyNotFoundException(string message)
            : base(message)
        {
        }
    }
}
