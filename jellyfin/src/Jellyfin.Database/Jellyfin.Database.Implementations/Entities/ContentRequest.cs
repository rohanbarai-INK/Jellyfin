using System;
using System.ComponentModel.DataAnnotations;
using Jellyfin.Database.Implementations.Enums;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Stores user content requests and their workflow state.
    /// </summary>
    public class ContentRequest
    {
        /// <summary>
        /// Gets or sets the request id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the request owner user id.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the raw request title.
        /// </summary>
        [MaxLength(255)]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the normalized request title.
        /// </summary>
        [MaxLength(255)]
        [StringLength(255)]
        public string NormalizedTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request type.
        /// </summary>
        public ContentRequestType Type { get; set; }

        /// <summary>
        /// Gets or sets the season number for series requests.
        /// </summary>
        public int? SeasonNumber { get; set; }

        /// <summary>
        /// Gets or sets when the request was created (UTC).
        /// </summary>
        public DateTime RequestedAt { get; set; }

        /// <summary>
        /// Gets or sets the workflow status.
        /// </summary>
        public ContentRequestStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the linked Jellyfin item id for completed requests.
        /// </summary>
        public Guid? JellyfinItemId { get; set; }

        /// <summary>
        /// Gets or sets how many times completion notification has been viewed.
        /// </summary>
        public int NotificationCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a pending request has been seen by admin.
        /// </summary>
        public bool IsAdminViewed { get; set; }

        /// <summary>
        /// Gets or sets request owner.
        /// </summary>
        public virtual User? User { get; set; }
    }
}
