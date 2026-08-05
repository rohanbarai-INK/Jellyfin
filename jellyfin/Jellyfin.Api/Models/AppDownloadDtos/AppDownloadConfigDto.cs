using System;

namespace Jellyfin.Api.Models.AppDownloadDtos
{
    /// <summary>
    /// DTO for app download configuration.
    /// </summary>
    public class AppDownloadConfigDto
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
        /// Gets or sets a value indicating whether the mobile APK is newly updated.
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
        /// Gets or sets a value indicating whether the TV APK is newly updated.
        /// </summary>
        public bool TvIsNew { get; set; }

        /// <summary>
        /// Gets or sets max interactions before NEW badge stops showing per user device.
        /// </summary>
        public int MaxNewInteractions { get; set; }

        /// <summary>
        /// Gets or sets last update timestamp in UTC.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets username of the admin who last saved the config.
        /// </summary>
        public string UpdatedByUsername { get; set; } = string.Empty;
    }
}
