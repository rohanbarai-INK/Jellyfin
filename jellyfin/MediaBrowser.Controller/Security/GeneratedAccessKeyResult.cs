using System;

namespace MediaBrowser.Controller.Security
{
    /// <summary>
    /// Result returned when creating an access key.
    /// </summary>
    public sealed class GeneratedAccessKeyResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GeneratedAccessKeyResult"/> class.
        /// </summary>
        /// <param name="key">The generated key.</param>
        /// <param name="durationMonths">The duration in months granted by the key.</param>
        /// <param name="createdAt">The creation date in UTC.</param>
        public GeneratedAccessKeyResult(string key, int durationMonths, DateTime createdAt)
        {
            Key = key;
            DurationMonths = durationMonths;
            CreatedAt = createdAt;
        }

        /// <summary>
        /// Gets the generated key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the duration in months granted by the key.
        /// </summary>
        public int DurationMonths { get; }

        /// <summary>
        /// Gets the key creation time in UTC.
        /// </summary>
        public DateTime CreatedAt { get; }
    }
}
