using System;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// An entity representing a redeemable access key.
    /// </summary>
    public class AccessKey
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AccessKey"/> class.
        /// </summary>
        /// <param name="key">The key value.</param>
        /// <param name="durationMonths">The duration in months granted by the key.</param>
        public AccessKey(string key, int durationMonths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            Id = Guid.NewGuid();
            Key = key.Trim().ToUpperInvariant();
            DurationMonths = durationMonths;
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets or sets the key id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the formatted key value.
        /// </summary>
        [MaxLength(32)]
        [StringLength(32)]
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the number of months granted by the key.
        /// </summary>
        public int DurationMonths { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this key has been redeemed.
        /// </summary>
        public bool IsRedeemed { get; set; }

        /// <summary>
        /// Gets or sets the user id that redeemed this key.
        /// </summary>
        public Guid? RedeemedByUserId { get; set; }

        /// <summary>
        /// Gets or sets the creation time in UTC.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the redemption time in UTC.
        /// </summary>
        public DateTime? RedeemedAt { get; set; }

        /// <summary>
        /// Gets or sets the user that redeemed this key.
        /// </summary>
        public virtual User? RedeemedByUser { get; set; }
    }
}
