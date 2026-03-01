using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Tracking;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Server.Implementations.Events.Consumers.Session
{
    /// <summary>
    /// Finalizes tracked watch sessions on playback stop.
    /// </summary>
    public class PlaybackStopTracker : IEventConsumer<PlaybackStopEventArgs>
    {
        private readonly WatchSessionTrackingService _trackingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaybackStopTracker"/> class.
        /// </summary>
        /// <param name="trackingService">Tracking service.</param>
        public PlaybackStopTracker(WatchSessionTrackingService trackingService)
        {
            _trackingService = trackingService;
        }

        /// <inheritdoc />
        public Task OnEvent(PlaybackStopEventArgs eventArgs)
            => _trackingService.HandlePlaybackStop(eventArgs);
    }
}
