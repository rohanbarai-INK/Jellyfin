using System;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.AppDownload
{
    /// <summary>
    /// Service for managing the admin-controlled app download configuration.
    /// </summary>
    public interface IAppDownloadService
    {
        /// <summary>
        /// Gets the current app download config.
        /// Returns defaults when no config has been saved yet.
        /// </summary>
        /// <returns>App download config info.</returns>
        Task<AppDownloadConfigInfo> GetConfigAsync();

        /// <summary>
        /// Saves the app download config.
        /// </summary>
        /// <param name="info">Updated config values.</param>
        /// <param name="actorUserId">User ID of the admin making the change.</param>
        /// <returns>Saved config info.</returns>
        Task<AppDownloadConfigInfo> SaveConfigAsync(AppDownloadConfigInfo info, Guid actorUserId);
    }
}
