using System;

namespace MediaBrowser.Model.System;

/// <summary>
/// Represents the current media mount auto-heal state exposed to clients.
/// </summary>
public class MediaMountAutoHealStatusInfo
{
    /// <summary>
    /// Gets or sets the overall state.
    /// </summary>
    public string State { get; set; } = "healthy";

    /// <summary>
    /// Gets or sets the user-facing message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether playback storage is healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether auto-heal is enabled.
    /// </summary>
    public bool IsAutoHealEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the service is running in mock mode.
    /// </summary>
    public bool IsMockMode { get; set; }

    /// <summary>
    /// Gets or sets the suggested client retry delay.
    /// </summary>
    public int RetryAfterSeconds { get; set; }

    /// <summary>
    /// Gets or sets the last failure reason.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Gets or sets the last checked timestamp.
    /// </summary>
    public DateTimeOffset? LastCheckedUtc { get; set; }

    /// <summary>
    /// Gets or sets the last restart request timestamp.
    /// </summary>
    public DateTimeOffset? LastRestartRequestedUtc { get; set; }

    /// <summary>
    /// Gets or sets the last recovered timestamp.
    /// </summary>
    public DateTimeOffset? LastRecoveredUtc { get; set; }
}
