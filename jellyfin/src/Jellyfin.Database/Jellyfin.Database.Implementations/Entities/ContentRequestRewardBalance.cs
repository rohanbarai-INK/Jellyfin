using System;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Stores extra admin-granted request balances for a user.
    /// </summary>
    public class ContentRequestRewardBalance
    {
        /// <summary>
        /// Gets or sets the user id.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets remaining rewarded movie request slots.
        /// </summary>
        public int MovieCount { get; set; }

        /// <summary>
        /// Gets or sets remaining rewarded series request slots.
        /// </summary>
        public int SeriesCount { get; set; }

        /// <summary>
        /// Gets or sets when this row was first created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets when this row was last updated.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the owning user.
        /// </summary>
        public virtual User? User { get; set; }
    }
}
