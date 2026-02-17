using System;

namespace MediaBrowser.Controller.Security
{
    /// <summary>
    /// Exception thrown when a key has already been redeemed.
    /// </summary>
    public sealed class AccessKeyAlreadyRedeemedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AccessKeyAlreadyRedeemedException"/> class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public AccessKeyAlreadyRedeemedException(string message)
            : base(message)
        {
        }
    }
}
