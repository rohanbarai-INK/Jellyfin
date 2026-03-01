using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Tracking;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Server.Implementations.Events.Consumers.Session
{
    /// <summary>
    /// Tracks playback-start events for watch-session analytics.
    /// </summary>
    public class PlaybackStartTracker : IEventConsumer<PlaybackStartEventArgs>
    {
        private readonly WatchSessionTrackingService _trackingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaybackStartTracker"/> class.
        /// </summary>
        /// <param name="trackingService">Tracking service.</param>
        public PlaybackStartTracker(WatchSessionTrackingService trackingService)
        {
            _trackingService = trackingService;
        }

        /// <inheritdoc />
        public Task OnEvent(PlaybackStartEventArgs eventArgs)
            => _trackingService.HandlePlaybackStart(eventArgs);
    }
}
