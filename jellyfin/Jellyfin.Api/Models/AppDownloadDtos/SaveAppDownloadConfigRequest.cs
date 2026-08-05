namespace Jellyfin.Api.Models.AppDownloadDtos
{
    /// <summary>
    /// Request body for saving the app download config.
    /// </summary>
    public class SaveAppDownloadConfigRequest
    {
        /// <summary>
        /// Gets or sets mobile APK download URL.
        /// </summary>
        public string MobileApkUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets mobile APK filename.
        /// </summary>
        public string MobileApkFileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the mobile APK should show a NEW badge.
        /// </summary>
        public bool MobileIsNew { get; set; }

        /// <summary>
        /// Gets or sets Android TV APK download URL.
        /// </summary>
        public string TvApkUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets TV APK filename.
        /// </summary>
        public string TvApkFileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the TV APK should show a NEW badge.
        /// </summary>
        public bool TvIsNew { get; set; }

        /// <summary>
        /// Gets or sets max times a user sees the NEW badge before it's suppressed. Minimum 1.
        /// </summary>
        public int MaxNewInteractions { get; set; } = 3;
    }
}
