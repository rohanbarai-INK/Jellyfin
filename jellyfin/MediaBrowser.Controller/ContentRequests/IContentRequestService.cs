using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.ContentRequests
{
    /// <summary>
    /// Handles content request workflows.
    /// </summary>
    public interface IContentRequestService
    {
        /// <summary>
        /// Creates a new content request.
        /// </summary>
        /// <param name="userId">The request owner user id.</param>
        /// <param name="title">The title.</param>
        /// <param name="type">The request type.</param>
        /// <param name="seasonNumber">The season number when type is series.</param>
        /// <returns>The created request.</returns>
        Task<ContentRequestInfo> CreateRequest(Guid userId, string title, ContentRequestType type, int? seasonNumber);

        /// <summary>
        /// Gets requests and quota for the current user.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <returns>The request list and quota summary.</returns>
        Task<MyContentRequestsResult> GetMyRequests(Guid userId);

        /// <summary>
        /// Gets paged request rows for the current user.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="skip">Rows to skip.</param>
        /// <param name="take">Rows to take.</param>
        /// <returns>Paged request rows.</returns>
        Task<ContentRequestListResult> GetMyRequestsPaged(Guid userId, int skip, int take);

        /// <summary>
        /// Gets public requests.
        /// </summary>
        /// <param name="skip">Rows to skip.</param>
        /// <param name="take">Rows to take.</param>
        /// <returns>Paged public requests.</returns>
        Task<ContentRequestListResult> GetPublicRequests(int skip, int take);

        /// <summary>
        /// Searches users for admin reward assignment suggestions.
        /// </summary>
        /// <param name="query">Search text.</param>
        /// <param name="take">Maximum rows.</param>
        /// <returns>User suggestions.</returns>
        Task<IReadOnlyList<ContentRequestUserSuggestion>> SearchUsersForAdmin(string query, int take);

        /// <summary>
        /// Gets request quota details for an arbitrary user as admin.
        /// </summary>
        /// <param name="userId">The target user id.</param>
        /// <returns>User quota details.</returns>
        Task<ContentRequestAdminUserQuotaResult> GetAdminUserQuota(Guid userId);

        /// <summary>
        /// Grants additional rewarded request slots to a user.
        /// </summary>
        /// <param name="userId">The target user id.</param>
        /// <param name="movieCount">Movie slots to add.</param>
        /// <param name="seriesCount">Series slots to add.</param>
        /// <returns>Updated user quota details.</returns>
        Task<ContentRequestAdminUserQuotaResult> GrantAdminRewardQuota(Guid userId, int movieCount, int seriesCount);

        /// <summary>
        /// Gets admin requests and marks unseen pending rows as viewed.
        /// </summary>
        /// <returns>Admin request list.</returns>
        Task<IReadOnlyList<ContentRequestInfo>> GetAdminRequests();

        /// <summary>
        /// Gets paged admin requests and marks unseen pending rows as viewed.
        /// </summary>
        /// <param name="skip">Rows to skip.</param>
        /// <param name="take">Rows to take.</param>
        /// <returns>Paged admin request rows.</returns>
        Task<ContentRequestListResult> GetAdminRequestsPaged(int skip, int take);

        /// <summary>
        /// Gets count of pending requests not yet viewed by admin.
        /// </summary>
        /// <returns>Unseen pending count.</returns>
        Task<int> GetAdminUnseenPendingCount();

        /// <summary>
        /// Approves a request.
        /// </summary>
        /// <param name="requestId">The request id.</param>
        /// <returns>The updated request.</returns>
        Task<ContentRequestInfo> Approve(Guid requestId);

        /// <summary>
        /// Rejects a request.
        /// </summary>
        /// <param name="requestId">The request id.</param>
        /// <returns>The updated request.</returns>
        Task<ContentRequestInfo> Reject(Guid requestId);

        /// <summary>
        /// Completes a request.
        /// </summary>
        /// <param name="requestId">The request id.</param>
        /// <param name="jellyfinItemId">The Jellyfin item id.</param>
        /// <returns>The updated request.</returns>
        Task<ContentRequestInfo> Complete(Guid requestId, Guid jellyfinItemId);

        /// <summary>
        /// Gets notifications for completed requests that are still visible.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <returns>Notification request rows.</returns>
        Task<IReadOnlyList<ContentRequestInfo>> GetNotifications(Guid userId);

        /// <summary>
        /// Marks notifications viewed in bulk by incrementing notification count.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="requestIds">The request ids.</param>
        /// <returns>A task.</returns>
        Task BulkMarkNotificationViewed(Guid userId, IReadOnlyCollection<Guid> requestIds);
    }
}
