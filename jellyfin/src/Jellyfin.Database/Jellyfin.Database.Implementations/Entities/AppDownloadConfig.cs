using System;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Database.Implementations.Entities
{
    /// <summary>
    /// Singleton config row for admin-managed app download links.
    /// </summary>
    public class AppDownloadConfig
    {
        /// <summary>
        /// Gets or sets primary key. Always "singleton".
        /// </summary>
        [MaxLength(16)]
        [StringLength(16)]
        public string Id { get; set; } = "singleton";

        /// <summary>
        /// Gets or sets mobile APK download URL.
        /// </summary>
        [MaxLength(2048)]
        [StringLength(2048)]
        public string MobileApkUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets mobile APK filename for the browser download prompt.
        /// </summary>
        [MaxLength(255)]
        [StringLength(255)]
        public string MobileApkFileName { get; set; } = "KnightFlix-v0.0.1.apk";

        /// <summary>
        /// Gets or sets a value indicating whether the mobile APK was recently updated.
        /// Causes a NEW badge to appear on the download button.
        /// </summary>
        public bool MobileIsNew { get; set; }

        /// <summary>
        /// Gets or sets Android TV APK download URL.
        /// </summary>
        [MaxLength(2048)]
        [StringLength(2048)]
        public string TvApkUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets TV APK filename for the browser download prompt.
        /// </summary>
        [MaxLength(255)]
        [StringLength(255)]
        public string TvApkFileName { get; set; } = "KnightFlixTV-v0.0.1.apk";

        /// <summary>
        /// Gets or sets a value indicating whether the TV APK was recently updated.
        /// Causes a NEW badge to appear on the download button.
        /// </summary>
        public bool TvIsNew { get; set; }

        /// <summary>
        /// Gets or sets the number of times a user may interact with a "New" badge
        /// before it stops appearing for them (per device, tracked client-side).
        /// Default is 3.
        /// </summary>
        public int MaxNewInteractions { get; set; } = 3;

        /// <summary>
        /// Gets or sets last update timestamp in UTC.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets username of the admin who last updated the config.
        /// </summary>
        [MaxLength(255)]
        [StringLength(255)]
        public string UpdatedByUsername { get; set; } = string.Empty;
    }
}
