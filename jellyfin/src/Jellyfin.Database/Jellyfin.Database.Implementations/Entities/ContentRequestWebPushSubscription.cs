using System;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Stores browser push subscriptions for request completion notifications.
    /// </summary>
    public class ContentRequestWebPushSubscription
    {
        /// <summary>
        /// Gets or sets the row id.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the user id.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the push endpoint.
        /// </summary>
        [MaxLength(2048)]
        [StringLength(2048)]
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the p256dh key.
        /// </summary>
        [MaxLength(512)]
        [StringLength(512)]
        public string P256dh { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the auth key.
        /// </summary>
        [MaxLength(512)]
        [StringLength(512)]
        public string Auth { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the UTC last update timestamp.
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the UTC last successful push send timestamp.
        /// </summary>
        public DateTime? LastNotifiedAt { get; set; }

        /// <summary>
        /// Gets or sets the subscription owner.
        /// </summary>
        public virtual User? User { get; set; }
    }
}
