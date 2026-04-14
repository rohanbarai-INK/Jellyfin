using System;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.ContentRequests
{
    /// <summary>
    /// Handles browser push subscriptions and request-completion push notifications.
    /// </summary>
    public interface IContentRequestWebPushService
    {
        /// <summary>
        /// Gets the configured VAPID public key for browser push subscriptions.
        /// </summary>
        /// <returns>The configured VAPID public key, or <c>null</c> when push is not configured.</returns>
        string? GetPublicVapidKey();

        /// <summary>
        /// Creates or updates a browser push subscription for a user.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="endpoint">The subscription endpoint.</param>
        /// <param name="p256dh">The p256dh key.</param>
        /// <param name="auth">The auth key.</param>
        /// <returns>A task.</returns>
        Task UpsertSubscription(Guid userId, string endpoint, string p256dh, string auth);

        /// <summary>
        /// Removes a browser push subscription for a user.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="endpoint">The subscription endpoint.</param>
        /// <returns>A task.</returns>
        Task RemoveSubscription(Guid userId, string endpoint);

        /// <summary>
        /// Sends request-completion browser push notifications for a completed request.
        /// </summary>
        /// <param name="request">The completed request.</param>
        /// <returns>A task.</returns>
        Task NotifyRequestCompleted(ContentRequestInfo request);
    }
}
