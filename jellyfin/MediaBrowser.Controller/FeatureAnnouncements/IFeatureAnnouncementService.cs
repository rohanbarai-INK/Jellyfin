using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.FeatureAnnouncements
{
    /// <summary>
    /// Handles admin-managed announcement campaigns.
    /// </summary>
    public interface IFeatureAnnouncementService
    {
        /// <summary>
        /// Gets all announcement campaigns for admin management.
        /// </summary>
        /// <returns>Announcement list.</returns>
        Task<IReadOnlyList<FeatureAnnouncementInfo>> GetAdminAnnouncements();

        /// <summary>
        /// Gets announcements currently eligible for end-user display.
        /// </summary>
        /// <param name="nowUtc">Current timestamp in UTC.</param>
        /// <returns>Active announcement list.</returns>
        Task<IReadOnlyList<FeatureAnnouncementInfo>> GetActiveAnnouncements(DateTime nowUtc);

        /// <summary>
        /// Creates or updates an announcement.
        /// </summary>
        /// <param name="options">Announcement payload.</param>
        /// <param name="actorUserId">Admin user id performing the update.</param>
        /// <returns>Updated announcement.</returns>
        Task<FeatureAnnouncementInfo> UpsertAnnouncement(FeatureAnnouncementUpsertInfo options, Guid actorUserId);
    }
}
