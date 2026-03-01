using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Tracking;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Server.Implementations.Events.Consumers.Session
{
    /// <summary>
    /// Tracks playback-progress events for watch-session analytics.
    /// </summary>
    public class PlaybackProgressTracker : IEventConsumer<PlaybackProgressEventArgs>
    {
        private readonly WatchSessionTrackingService _trackingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaybackProgressTracker"/> class.
        /// </summary>
        /// <param name="trackingService">Tracking service.</param>
        public PlaybackProgressTracker(WatchSessionTrackingService trackingService)
        {
            _trackingService = trackingService;
        }

        /// <inheritdoc />
        public Task OnEvent(PlaybackProgressEventArgs eventArgs)
            => _trackingService.HandlePlaybackProgress(eventArgs);
    }
}
