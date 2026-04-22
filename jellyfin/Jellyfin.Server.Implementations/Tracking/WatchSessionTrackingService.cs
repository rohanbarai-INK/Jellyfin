using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Tracking
{
    /// <summary>
    /// Tracks in-flight playback events and persists finalized watch sessions.
    /// </summary>
    public class WatchSessionTrackingService
    {
        private const int _suspicionThreshold = 5;
        private const double _highPlaybackSpeedThreshold = 1.5D;
        private const long _minimumExpectedIntervalTicks = TimeSpan.TicksPerSecond;
        private const long _maximumSessionTicks = TimeSpan.TicksPerHour * 8;
        private const long _resumePositionThresholdTicks = TimeSpan.TicksPerMinute;
        private const long _resumeBaselineWindowTicks = TimeSpan.TicksPerSecond * 15;
        private const long _seekJitterToleranceTicks = TimeSpan.TicksPerSecond * 3;
        private const long _suspiciousSeekJumpTicks = TimeSpan.TicksPerMinute * 5;
        private const int _extremeForwardJumpMultiplier = 4;

        private readonly ConcurrentDictionary<string, ActiveWatchState> _activeSessions = new(StringComparer.Ordinal);
        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
        private readonly WatchSessionAggregationService _aggregationService;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<WatchSessionTrackingService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WatchSessionTrackingService"/> class.
        /// </summary>
        /// <param name="dbProvider">Database provider.</param>
        /// <param name="aggregationService">Aggregation service.</param>
        /// <param name="timeProvider">Time provider.</param>
        /// <param name="logger">Logger.</param>
        public WatchSessionTrackingService(
            IDbContextFactory<JellyfinDbContext> dbProvider,
            WatchSessionAggregationService aggregationService,
            TimeProvider timeProvider,
            ILogger<WatchSessionTrackingService> logger)
        {
            _dbProvider = dbProvider;
            _aggregationService = aggregationService;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>
        /// Handles playback-start events.
        /// </summary>
        /// <param name="eventArgs">Playback-start event args.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task HandlePlaybackStart(PlaybackStartEventArgs eventArgs)
        {
            ArgumentNullException.ThrowIfNull(eventArgs);
            var nowUtc = GetUtcNow();
            var users = ResolveUsers(eventArgs);
            var itemId = ResolveItemId(eventArgs);
            if (users.Count == 0 || itemId.Equals(Guid.Empty))
            {
                return Task.CompletedTask;
            }

            var trackingSessionId = ResolveTrackingSessionId(eventArgs);
            var persistedSessionId = ResolvePersistedSessionId(eventArgs);
            var initialPositionTicks = NormalizePositionTicks(eventArgs.PlaybackPositionTicks);
            foreach (var user in users)
            {
                var key = BuildSessionKey(user.Id, itemId, trackingSessionId);
                var session = CreateSession(user.Id, itemId, persistedSessionId, nowUtc);
                var state = new ActiveWatchState(session, initialPositionTicks, nowUtc);
                _activeSessions.AddOrUpdate(key, state, (_, _) => state);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles playback-progress events.
        /// </summary>
        /// <param name="eventArgs">Playback-progress event args.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task HandlePlaybackProgress(PlaybackProgressEventArgs eventArgs)
        {
            ArgumentNullException.ThrowIfNull(eventArgs);
            if (eventArgs.IsAutomated)
            {
                // SessionInfo's 1s synthetic updates are intended for playstate continuity.
                // Counting them alongside client reports causes duplicate/irregular deltas and false suspicion.
                return Task.CompletedTask;
            }

            var nowUtc = GetUtcNow();
            var users = ResolveUsers(eventArgs);
            var itemId = ResolveItemId(eventArgs);
            if (users.Count == 0 || itemId.Equals(Guid.Empty))
            {
                return Task.CompletedTask;
            }

            var trackingSessionId = ResolveTrackingSessionId(eventArgs);
            var persistedSessionId = ResolvePersistedSessionId(eventArgs);
            var currentPositionTicks = NormalizePositionTicks(eventArgs.PlaybackPositionTicks);
            foreach (var user in users)
            {
                var key = BuildSessionKey(user.Id, itemId, trackingSessionId);
                var state = _activeSessions.GetOrAdd(key, _ => new ActiveWatchState(
                    CreateSession(user.Id, itemId, persistedSessionId, nowUtc),
                    currentPositionTicks,
                    nowUtc));

                state.ExecuteLocked(() =>
                {
                    UpdateSessionMetadata(state.Session, persistedSessionId);
                    ApplyProgressDelta(state, currentPositionTicks, eventArgs.IsPaused, nowUtc, isFinalProgress: false);
                });
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles playback-stop events, persists finalized sessions and triggers aggregation.
        /// </summary>
        /// <param name="eventArgs">Playback-stop event args.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task HandlePlaybackStop(PlaybackStopEventArgs eventArgs)
        {
            ArgumentNullException.ThrowIfNull(eventArgs);
            var nowUtc = GetUtcNow();
            var users = ResolveUsers(eventArgs);
            var itemId = ResolveItemId(eventArgs);
            if (users.Count == 0 || itemId.Equals(Guid.Empty))
            {
                return;
            }

            var trackingSessionId = ResolveTrackingSessionId(eventArgs);
            var persistedSessionId = ResolvePersistedSessionId(eventArgs);
            var finalPositionTicks = NormalizePositionTicks(eventArgs.PlaybackPositionTicks);
            foreach (var user in users)
            {
                var key = BuildSessionKey(user.Id, itemId, trackingSessionId);
                if (!_activeSessions.TryRemove(key, out var state))
                {
                    state = new ActiveWatchState(
                        CreateSession(user.Id, itemId, persistedSessionId, nowUtc),
                        finalPositionTicks,
                        nowUtc);
                }

                state.ExecuteLocked(() =>
                {
                    UpdateSessionMetadata(state.Session, persistedSessionId);
                    ApplyProgressDelta(state, finalPositionTicks, isPaused: false, nowUtc, isFinalProgress: true);
                    FinalizeSession(state.Session, nowUtc);
                });

                await PersistSessionAsync(state.Session).ConfigureAwait(false);
                await _aggregationService.ProcessSession(state.Session, eventArgs.PlayedToCompletion).ConfigureAwait(false);
            }
        }

        private static List<User> ResolveUsers(PlaybackProgressEventArgs eventArgs)
            => eventArgs.Users
                .Where(user => !user.Id.Equals(Guid.Empty))
                .GroupBy(user => user.Id)
                .Select(group => group.First())
                .ToList();

        private static Guid ResolveItemId(PlaybackProgressEventArgs eventArgs)
        {
            if (eventArgs.Item is not null && !eventArgs.Item.Id.Equals(Guid.Empty))
            {
                return eventArgs.Item.Id;
            }

            if (eventArgs.MediaInfo is not null && !eventArgs.MediaInfo.Id.Equals(Guid.Empty))
            {
                return eventArgs.MediaInfo.Id;
            }

            return Guid.Empty;
        }

        private static string ResolveTrackingSessionId(PlaybackProgressEventArgs eventArgs)
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.PlaySessionId))
            {
                return eventArgs.PlaySessionId;
            }

            if (!string.IsNullOrWhiteSpace(eventArgs.Session?.Id))
            {
                return eventArgs.Session.Id;
            }

            if (!string.IsNullOrWhiteSpace(eventArgs.DeviceId))
            {
                return eventArgs.DeviceId;
            }

            return "unknown";
        }

        private static string ResolvePersistedSessionId(PlaybackProgressEventArgs eventArgs)
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.PlaySessionId))
            {
                return eventArgs.PlaySessionId;
            }

            if (!string.IsNullOrWhiteSpace(eventArgs.Session?.Id))
            {
                return eventArgs.Session.Id;
            }

            if (!string.IsNullOrWhiteSpace(eventArgs.DeviceId))
            {
                return eventArgs.DeviceId;
            }

            return "unknown";
        }

        private static string BuildSessionKey(Guid userId, Guid itemId, string sessionId)
            => $"{userId:N}:{itemId:N}:{sessionId}";

        private static long NormalizePositionTicks(long? positionTicks)
            => Math.Max(0, positionTicks ?? 0);

        private static long CapTicks(long ticks)
            => Math.Min(_maximumSessionTicks, Math.Max(0, ticks));

        private static string LimitText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        private UserWatchSession CreateSession(
            Guid userId,
            Guid itemId,
            string persistedSessionId,
            DateTime nowUtc)
            => new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ItemId = itemId,
                SessionId = LimitText(persistedSessionId, 128),
                StartTimeUtc = nowUtc,
                EndTimeUtc = null,
                AccumulatedTicks = 0,
                ValidatedTicks = 0,
                PlaybackSpeed = 1D,
                IsValidSession = true,
                SuspicionScore = 0
            };

        private static void UpdateSessionMetadata(UserWatchSession session, string persistedSessionId)
        {
            session.SessionId = LimitText(persistedSessionId, 128);
        }

        private static void ApplyProgressDelta(
            ActiveWatchState state,
            long currentPositionTicks,
            bool isPaused,
            DateTime nowUtc,
            bool isFinalProgress)
        {
            var deltaTicks = currentPositionTicks - state.LastPositionTicks;
            if (isPaused)
            {
                state.LastPositionTicks = currentPositionTicks;
                state.LastProgressUtc = nowUtc;
                return;
            }

            if (state.Session.AccumulatedTicks == 0
                && state.Session.ValidatedTicks == 0
                && state.LastPositionTicks == 0
                && (nowUtc - state.LastProgressUtc).Ticks <= _resumeBaselineWindowTicks
                && currentPositionTicks >= _resumePositionThresholdTicks)
            {
                // Some clients do not include resume offsets in playback-start events.
                // Treat the first large progress update as baseline instead of suspicious seek abuse.
                state.LastPositionTicks = currentPositionTicks;
                state.LastProgressUtc = nowUtc;
                return;
            }

            if (deltaTicks <= 0)
            {
                if (deltaTicks < -_seekJitterToleranceTicks
                    && Math.Abs(deltaTicks) >= _suspiciousSeekJumpTicks)
                {
                    state.Session.SuspicionScore++;
                }

                state.LastPositionTicks = currentPositionTicks;
                state.LastProgressUtc = nowUtc;
                return;
            }

            state.Session.AccumulatedTicks = CapTicks(state.Session.AccumulatedTicks + deltaTicks);
            var elapsedTicks = Math.Max(_minimumExpectedIntervalTicks, (nowUtc - state.LastProgressUtc).Ticks);
            var maxAllowedDeltaTicks = elapsedTicks * 2;

            if (deltaTicks <= maxAllowedDeltaTicks)
            {
                var playbackSpeed = deltaTicks / (double)elapsedTicks;
                if (double.IsFinite(playbackSpeed) && playbackSpeed > 0)
                {
                    state.Session.PlaybackSpeed = playbackSpeed;
                }

                var validatedDeltaTicks = deltaTicks;
                if (state.Session.PlaybackSpeed > _highPlaybackSpeedThreshold)
                {
                    validatedDeltaTicks = (long)Math.Floor(validatedDeltaTicks / state.Session.PlaybackSpeed);
                }

                if (validatedDeltaTicks > 0)
                {
                    state.Session.ValidatedTicks = CapTicks(state.Session.ValidatedTicks + validatedDeltaTicks);
                }
            }
            else
            {
                if (isFinalProgress)
                {
                    // Some clients report a coarse final position only at stop.
                    // Recover plausible validated time based on total session wall-clock.
                    var sessionElapsedTicks = Math.Max(_minimumExpectedIntervalTicks, (nowUtc - state.Session.StartTimeUtc).Ticks);
                    var maxSessionValidatedTicks = CapTicks((long)Math.Floor(sessionElapsedTicks * _highPlaybackSpeedThreshold));
                    var remainingValidatedBudget = Math.Max(0, maxSessionValidatedTicks - state.Session.ValidatedTicks);
                    var fallbackValidatedTicks = Math.Min(deltaTicks, remainingValidatedBudget);
                    if (fallbackValidatedTicks > 0)
                    {
                        state.Session.ValidatedTicks = CapTicks(state.Session.ValidatedTicks + fallbackValidatedTicks);
                    }
                }

                var suspiciousDeltaThreshold = Math.Max(
                    maxAllowedDeltaTicks * _extremeForwardJumpMultiplier,
                    _suspiciousSeekJumpTicks);

                if (deltaTicks >= suspiciousDeltaThreshold)
                {
                    state.Session.SuspicionScore++;
                }
            }

            state.LastPositionTicks = currentPositionTicks;
            state.LastProgressUtc = nowUtc;
        }

        private static void FinalizeSession(UserWatchSession session, DateTime endTimeUtc)
        {
            session.EndTimeUtc = endTimeUtc;

            if (session.AccumulatedTicks >= _maximumSessionTicks)
            {
                session.AccumulatedTicks = _maximumSessionTicks;
                session.SuspicionScore++;
            }

            if (session.ValidatedTicks >= _maximumSessionTicks)
            {
                session.ValidatedTicks = _maximumSessionTicks;
                session.SuspicionScore++;
            }

            if (endTimeUtc - session.StartTimeUtc > TimeSpan.FromHours(6))
            {
                session.SuspicionScore++;
            }

            session.IsValidSession = session.SuspicionScore <= _suspicionThreshold;
        }

        private DateTime GetUtcNow()
            => _timeProvider.GetUtcNow().UtcDateTime;

        private async Task PersistSessionAsync(UserWatchSession session)
        {
            try
            {
                var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
                await using (dbContext.ConfigureAwait(false))
                {
                    dbContext.UserWatchSessions.Add(session);
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to persist watch session {SessionId} for user {UserId}.", session.SessionId, session.UserId);
                throw;
            }
        }

        private sealed class ActiveWatchState
        {
            private readonly object _syncLock = new();

            public ActiveWatchState(UserWatchSession session, long lastPositionTicks, DateTime lastProgressUtc)
            {
                Session = session;
                LastPositionTicks = lastPositionTicks;
                LastProgressUtc = lastProgressUtc;
            }

            public UserWatchSession Session { get; }

            public long LastPositionTicks { get; set; }

            public DateTime LastProgressUtc { get; set; }

            public void ExecuteLocked(Action action)
            {
                lock (_syncLock)
                {
                    action();
                }
            }
        }
    }
}
