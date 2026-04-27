using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.System;

namespace MediaBrowser.Controller.AutoHeal;

/// <summary>
/// Exposes media mount auto-heal status and repair hooks.
/// </summary>
public interface IMediaMountAutoHealService
{
    /// <summary>
    /// Gets the current auto-heal status, triggering a mount evaluation when needed.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current auto-heal status.</returns>
    Task<MediaMountAutoHealStatusInfo> GetStatusAsync(CancellationToken cancellationToken = default);
}
