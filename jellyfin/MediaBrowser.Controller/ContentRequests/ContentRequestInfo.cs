using System;

namespace MediaBrowser.Controller.ContentRequests
{
    /// <summary>
    /// Content request row model.
    /// </summary>
    public sealed class ContentRequestInfo
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
        /// Gets or sets the request owner username when available.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the normalized title.
        /// </summary>
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
        /// Gets or sets when request was created.
        /// </summary>
        public DateTime RequestedAt { get; set; }

        /// <summary>
        /// Gets or sets request status.
        /// </summary>
        public ContentRequestStatus Status { get; set; }

        /// <summary>
        /// Gets or sets completed Jellyfin item id.
        /// </summary>
        public Guid? JellyfinItemId { get; set; }

        /// <summary>
        /// Gets or sets notification view counter.
        /// </summary>
        public int NotificationCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a pending request is admin-viewed.
        /// </summary>
        public bool IsAdminViewed { get; set; }
    }
}
