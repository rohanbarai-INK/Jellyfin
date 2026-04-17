using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Achievements;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Leaderboard;
using MediaBrowser.Controller.Library;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Tracking
{
    /// <summary>
    /// Aggregates validated watch sessions into period-based insight tables.
    /// </summary>
    public class WatchSessionAggregationService
    {
        private const string _allPeriodKey = "ALL";
        private static readonly TimeZoneInfo _insightsTimeZone = ResolveInsightsTimeZone();

        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
        private readonly IAchievementService? _achievementService;
        private readonly ILeaderboardService? _leaderboardService;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<WatchSessionAggregationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WatchSessionAggregationService"/> class.
        /// </summary>
        /// <param name="dbProvider">Database provider.</param>
        /// <param name="achievementService">Achievement service.</param>
        /// <param name="leaderboardService">Leaderboard service.</param>
        /// <param name="libraryManager">Library manager.</param>
        /// <param name="logger">Logger.</param>
        public WatchSessionAggregationService(
            IDbContextFactory<JellyfinDbContext> dbProvider,
            IAchievementService? achievementService,
            ILeaderboardService? leaderboardService,
            ILibraryManager libraryManager,
            ILogger<WatchSessionAggregationService> logger)
        {
            _dbProvider = dbProvider;
            _achievementService = achievementService;
            _leaderboardService = leaderboardService;
            _libraryManager = libraryManager;
            _logger = logger;
        }

        /// <summary>
        /// Processes a finalized watch session into aggregated tables.
        /// </summary>
        /// <param name="session">Finalized session.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task ProcessSession(UserWatchSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            if (!session.IsValidSession || session.ValidatedTicks <= 0)
            {
                return;
            }

            var item = _libraryManager.GetItemById(session.ItemId);
            if (item is null)
            {
                _logger.LogDebug("Skipping aggregation for session {SessionId}; item {ItemId} is unavailable.", session.Id, session.ItemId);
                return;
            }

            var runtimeTicks = Math.Max(0, item.RunTimeTicks ?? 0);
            var isMovie = item is Movie;
            var isEpisode = item is Episode;
            var isCompleted = runtimeTicks > 0 && session.ValidatedTicks >= (long)Math.Floor(runtimeTicks * 0.9D);
            var hourlyTickDistribution = BuildHourlyTickDistribution(session);
            var hourBuckets = hourlyTickDistribution.Keys.ToArray();
            var genres = item.Genres
                .Where(genre => !string.IsNullOrWhiteSpace(genre))
                .Select(static genre => genre.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var periodDescriptors = BuildPeriods(session.StartTimeUtc).ToArray();
            var periodTypes = periodDescriptors.Select(period => period.PeriodType).ToArray();
            var periodKeys = periodDescriptors.Select(period => period.PeriodKey).ToArray();

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var bingeInfo = item is Episode currentEpisode
                    ? await ResolveBingeInfoAsync(dbContext, session, currentEpisode).ConfigureAwait(false)
                    : null;

                var transaction = await dbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
                await using (transaction.ConfigureAwait(false))
                {
                    var existingPeriodStats = await dbContext.UserPeriodStats
                        .Where(stats => stats.UserId.Equals(session.UserId)
                            && periodTypes.Contains(stats.PeriodType)
                            && periodKeys.Contains(stats.PeriodKey))
                        .ToListAsync()
                        .ConfigureAwait(false);

                    var existingHourlyStats = await dbContext.UserPeriodHourlyStats
                        .Where(stats => stats.UserId.Equals(session.UserId)
                            && periodTypes.Contains(stats.PeriodType)
                            && periodKeys.Contains(stats.PeriodKey)
                            && hourBuckets.Contains(stats.Hour))
                        .ToListAsync()
                        .ConfigureAwait(false);

                    var existingGenreStats = genres.Length == 0
                        ? []
                        : await dbContext.UserGenrePeriodStats
                            .Where(stats => stats.UserId.Equals(session.UserId)
                                && periodTypes.Contains(stats.PeriodType)
                                && periodKeys.Contains(stats.PeriodKey)
                                && genres.Contains(stats.GenreId))
                            .ToListAsync()
                            .ConfigureAwait(false);

                    foreach (var period in periodDescriptors)
                    {
                        var periodStats = existingPeriodStats.FirstOrDefault(stats =>
                            stats.PeriodType == period.PeriodType && stats.PeriodKey == period.PeriodKey);
                        if (periodStats is null)
                        {
                            periodStats = new UserPeriodStats
                            {
                                Id = Guid.NewGuid(),
                                UserId = session.UserId,
                                PeriodType = period.PeriodType,
                                PeriodKey = period.PeriodKey,
                                PeriodStartUtc = period.PeriodStartUtc,
                                PeriodEndUtc = period.PeriodEndUtc
                            };

                            existingPeriodStats.Add(periodStats);
                            dbContext.UserPeriodStats.Add(periodStats);
                        }
                        else
                        {
                            periodStats.PeriodStartUtc = period.PeriodStartUtc;
                            periodStats.PeriodEndUtc = period.PeriodEndUtc;
                        }

                        periodStats.TotalValidatedTicks += session.ValidatedTicks;
                        periodStats.SessionCount++;

                        if (isCompleted)
                        {
                            if (isMovie)
                            {
                                periodStats.CompletedMovies++;
                            }
                            else if (isEpisode)
                            {
                                periodStats.CompletedEpisodes++;
                            }
                        }

                        if (bingeInfo is not null)
                        {
                            periodStats.BingeSessions++;
                        }

                        foreach (var hourAllocation in hourlyTickDistribution)
                        {
                            if (hourAllocation.Value <= 0)
                            {
                                continue;
                            }

                            var hourlyStats = existingHourlyStats.FirstOrDefault(stats =>
                                stats.PeriodType == period.PeriodType
                                && stats.PeriodKey == period.PeriodKey
                                && stats.Hour == hourAllocation.Key);
                            if (hourlyStats is null)
                            {
                                hourlyStats = new UserPeriodHourlyStats
                                {
                                    Id = Guid.NewGuid(),
                                    UserId = session.UserId,
                                    PeriodType = period.PeriodType,
                                    PeriodKey = period.PeriodKey,
                                    Hour = hourAllocation.Key
                                };

                                existingHourlyStats.Add(hourlyStats);
                                dbContext.UserPeriodHourlyStats.Add(hourlyStats);
                            }

                            hourlyStats.TotalValidatedTicks += hourAllocation.Value;
                        }
                    }

                    if (genres.Length > 0)
                    {
                        foreach (var genre in genres)
                        {
                            foreach (var period in periodDescriptors)
                            {
                                var genreStats = existingGenreStats.FirstOrDefault(stats =>
                                    stats.PeriodType == period.PeriodType
                                    && stats.PeriodKey == period.PeriodKey
                                    && stats.GenreId.Equals(genre, StringComparison.OrdinalIgnoreCase));
                                if (genreStats is null)
                                {
                                    genreStats = new UserGenrePeriodStats
                                    {
                                        Id = Guid.NewGuid(),
                                        UserId = session.UserId,
                                        PeriodType = period.PeriodType,
                                        PeriodKey = period.PeriodKey,
                                        GenreId = genre
                                    };

                                    existingGenreStats.Add(genreStats);
                                    dbContext.UserGenrePeriodStats.Add(genreStats);
                                }

                                genreStats.TotalValidatedTicks += session.ValidatedTicks;
                            }
                        }
                    }

                    if (bingeInfo is not null)
                    {
                        dbContext.UserBingeSessions.Add(new UserBingeSession
                        {
                            Id = Guid.NewGuid(),
                            UserId = session.UserId,
                            SessionDateUtc = DateTime.SpecifyKind(session.StartTimeUtc, DateTimeKind.Utc),
                            SeriesId = bingeInfo.SeriesId,
                            EpisodeCount = bingeInfo.EpisodeCount,
                            TotalWatchTicks = bingeInfo.TotalWatchTicks
                        });
                    }

                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                    await transaction.CommitAsync().ConfigureAwait(false);
                }
            }

            if (_leaderboardService is not null)
            {
                try
                {
                    var validatedMinutes = session.ValidatedTicks / TimeSpan.TicksPerMinute;
                    var sessionSeasonYear = DateTime.SpecifyKind(session.StartTimeUtc, DateTimeKind.Utc).Year;
                    await _leaderboardService.RecordPlaybackStats(
                        session.UserId,
                        sessionSeasonYear,
                        validatedMinutes,
                        isMovie && isCompleted,
                        isEpisode && isCompleted,
                        genres).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Leaderboard playback recording failed for user {UserId}.", session.UserId);
                }
            }

            if (_achievementService is null)
            {
                return;
            }

            try
            {
                await _achievementService.Sync(session.UserId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Achievement sync failed after watch aggregation for user {UserId}.", session.UserId);
            }
        }

        private static PeriodDescriptor[] BuildPeriods(DateTime startTimeUtc)
        {
            var utcStart = DateTime.SpecifyKind(startTimeUtc, DateTimeKind.Utc);
            var monthStart = new DateTime(utcStart.Year, utcStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var yearStart = new DateTime(utcStart.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            return
            [
                new PeriodDescriptor(
                    PeriodType.Month,
                    monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    monthStart,
                    monthStart.AddMonths(1)),
                new PeriodDescriptor(
                    PeriodType.Year,
                    yearStart.ToString("yyyy", CultureInfo.InvariantCulture),
                    yearStart,
                    yearStart.AddYears(1)),
                new PeriodDescriptor(
                    PeriodType.AllTime,
                    _allPeriodKey,
                    DateTime.UnixEpoch,
                    DateTime.MaxValue)
            ];
        }

        private static Dictionary<int, long> BuildHourlyTickDistribution(UserWatchSession session)
        {
            var startUtc = DateTime.SpecifyKind(session.StartTimeUtc, DateTimeKind.Utc);
            var inferredEndUtc = startUtc.AddTicks(Math.Max(1, session.ValidatedTicks));
            var endUtc = session.EndTimeUtc.HasValue
                ? DateTime.SpecifyKind(session.EndTimeUtc.Value, DateTimeKind.Utc)
                : inferredEndUtc;
            if (endUtc <= startUtc)
            {
                endUtc = inferredEndUtc;
            }

            var startLocal = TimeZoneInfo.ConvertTimeFromUtc(startUtc, _insightsTimeZone);
            var endLocal = TimeZoneInfo.ConvertTimeFromUtc(endUtc, _insightsTimeZone);
            var segments = SliceIntoHourlySegments(startLocal, endLocal);
            var totalDurationTicks = segments.Sum(static segment => segment.DurationTicks);
            if (totalDurationTicks <= 0)
            {
                return new Dictionary<int, long>
                {
                    [startLocal.Hour] = session.ValidatedTicks
                };
            }

            var byHour = new Dictionary<int, long>();
            var remainingValidatedTicks = session.ValidatedTicks;
            var remainingDurationTicks = totalDurationTicks;
            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                long distributedTicks;
                if (index == segments.Count - 1 || remainingDurationTicks <= 0)
                {
                    distributedTicks = remainingValidatedTicks;
                }
                else
                {
                    distributedTicks = (long)Math.Floor((decimal)remainingValidatedTicks * segment.DurationTicks / remainingDurationTicks);
                }

                remainingValidatedTicks -= distributedTicks;
                remainingDurationTicks -= segment.DurationTicks;

                if (distributedTicks <= 0)
                {
                    continue;
                }

                if (!byHour.TryAdd(segment.Hour, distributedTicks))
                {
                    byHour[segment.Hour] += distributedTicks;
                }
            }

            return byHour;
        }

        private static TimeZoneInfo ResolveInsightsTimeZone()
        {
            // Keep Personal Insights bucketing aligned to Indian user expectations.
            foreach (var candidateId in new[] { "Asia/Kolkata", "India Standard Time" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(candidateId);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.Utc;
        }

        private static List<HourSegment> SliceIntoHourlySegments(DateTime startUtc, DateTime endUtc)
        {
            var segments = new List<HourSegment>();
            var cursor = startUtc;
            while (cursor < endUtc)
            {
                var nextHour = new DateTime(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
                var segmentEnd = nextHour < endUtc ? nextHour : endUtc;
                var durationTicks = (segmentEnd - cursor).Ticks;
                if (durationTicks > 0)
                {
                    segments.Add(new HourSegment(cursor.Hour, durationTicks));
                }

                cursor = segmentEnd;
            }

            return segments;
        }

        private static async Task<BingeInfo?> ResolveBingeInfoAsync(JellyfinDbContext dbContext, UserWatchSession session, Episode currentEpisode)
        {
            if (currentEpisode.SeriesId.Equals(Guid.Empty)
                || !currentEpisode.ParentIndexNumber.HasValue
                || !currentEpisode.IndexNumber.HasValue)
            {
                return null;
            }

            var expectedSeason = currentEpisode.ParentIndexNumber.Value;
            var expectedEpisode = currentEpisode.IndexNumber.Value - 1;
            if (expectedEpisode <= 0)
            {
                return null;
            }

            var previousEpisodes = await (
                    from watchSession in dbContext.UserWatchSessions.AsNoTracking()
                    join item in dbContext.BaseItems.AsNoTracking() on watchSession.ItemId equals item.Id
                    where watchSession.UserId.Equals(session.UserId)
                          && watchSession.IsValidSession
                          && watchSession.StartTimeUtc < session.StartTimeUtc
                          && item.Type == nameof(Episode)
                          && item.SeriesId.HasValue
                          && item.SeriesId.Value.Equals(currentEpisode.SeriesId)
                          && item.ParentIndexNumber.HasValue
                          && item.IndexNumber.HasValue
                    orderby watchSession.StartTimeUtc descending
                    select new
                    {
                        watchSession.ValidatedTicks,
                        Season = item.ParentIndexNumber!.Value,
                        Episode = item.IndexNumber!.Value
                    })
                .Take(20)
                .ToListAsync()
                .ConfigureAwait(false);

            var streakCount = 1;
            var streakTicks = session.ValidatedTicks;
            foreach (var previousEpisode in previousEpisodes)
            {
                if (previousEpisode.Season != expectedSeason || previousEpisode.Episode != expectedEpisode)
                {
                    break;
                }

                streakCount++;
                streakTicks += previousEpisode.ValidatedTicks;
                expectedEpisode--;
                if (expectedEpisode <= 0)
                {
                    break;
                }
            }

            if (streakCount < 3)
            {
                return null;
            }

            return new BingeInfo(currentEpisode.SeriesId, streakCount, streakTicks);
        }

        private sealed record PeriodDescriptor(
            PeriodType PeriodType,
            string PeriodKey,
            DateTime PeriodStartUtc,
            DateTime PeriodEndUtc);

        private sealed record HourSegment(int Hour, long DurationTicks);

        private sealed record BingeInfo(Guid SeriesId, int EpisodeCount, long TotalWatchTicks);
    }
}
