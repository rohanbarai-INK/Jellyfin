using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.PersonalInsights;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Server.Implementations.Tracking
{
    /// <summary>
    /// Provides personal insight read models from aggregated tables.
    /// </summary>
    public class PersonalInsightsService : IPersonalInsightsService
    {
        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="PersonalInsightsService"/> class.
        /// </summary>
        /// <param name="dbProvider">Database provider.</param>
        /// <param name="timeProvider">Time provider.</param>
        public PersonalInsightsService(IDbContextFactory<JellyfinDbContext> dbProvider, TimeProvider timeProvider)
        {
            _dbProvider = dbProvider;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc />
        public async Task<PersonalInsightsResult> GetInsights(Guid userId, PersonalInsightsPeriodType periodType)
        {
            if (userId.Equals(Guid.Empty))
            {
                throw new ArgumentException("User id cannot be empty.", nameof(userId));
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var currentPeriod = ResolveCurrentPeriod(periodType, nowUtc);
            var previousPeriod = ResolvePreviousPeriod(periodType, nowUtc);
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var statsKeys = previousPeriod is null
                    ? new[] { currentPeriod.PeriodKey }
                    : new[] { currentPeriod.PeriodKey, previousPeriod.Value.PeriodKey };

                var periodStats = await dbContext.UserPeriodStats
                    .AsNoTracking()
                    .Where(stats => stats.UserId.Equals(userId)
                        && stats.PeriodType == currentPeriod.PeriodType
                        && statsKeys.Contains(stats.PeriodKey))
                    .ToListAsync()
                    .ConfigureAwait(false);

                var currentStats = periodStats.FirstOrDefault(stats => stats.PeriodKey == currentPeriod.PeriodKey);
                var previousStats = previousPeriod is null
                    ? null
                    : periodStats.FirstOrDefault(stats => stats.PeriodKey == previousPeriod.Value.PeriodKey);

                var hourlyRows = await dbContext.UserPeriodHourlyStats
                    .AsNoTracking()
                    .Where(stats => stats.UserId.Equals(userId)
                        && stats.PeriodType == currentPeriod.PeriodType
                        && stats.PeriodKey == currentPeriod.PeriodKey)
                    .ToListAsync()
                    .ConfigureAwait(false);

                var topGenres = await dbContext.UserGenrePeriodStats
                    .AsNoTracking()
                    .Where(stats => stats.UserId.Equals(userId)
                        && stats.PeriodType == currentPeriod.PeriodType
                        && stats.PeriodKey == currentPeriod.PeriodKey)
                    .OrderByDescending(stats => stats.TotalValidatedTicks)
                    .Take(3)
                    .ToListAsync()
                    .ConfigureAwait(false);

                var bingeRows = await dbContext.UserBingeSessions
                    .AsNoTracking()
                    .Where(binge => binge.UserId.Equals(userId)
                        && binge.SessionDateUtc >= currentPeriod.PeriodStartUtc
                        && binge.SessionDateUtc < currentPeriod.PeriodEndUtc)
                    .OrderByDescending(binge => binge.SessionDateUtc)
                    .Take(20)
                    .ToListAsync()
                    .ConfigureAwait(false);

                var recentBinges = bingeRows
                    .OrderByDescending(binge => binge.SessionDateUtc)
                    .Take(3)
                    .ToList();
                var bingeSeriesIds = recentBinges
                    .Select(binge => binge.SeriesId)
                    .Distinct()
                    .ToArray();
                var bingeSeriesNames = bingeSeriesIds.Length == 0
                    ? new System.Collections.Generic.Dictionary<Guid, string>()
                    : await dbContext.BaseItems
                        .AsNoTracking()
                        .Where(item => bingeSeriesIds.Contains(item.Id))
                        .Select(item => new { item.Id, item.Name })
                        .ToDictionaryAsync(item => item.Id, item => item.Name ?? string.Empty)
                        .ConfigureAwait(false);

                var continueWatchingCandidates = await (
                        from userData in dbContext.UserData.AsNoTracking()
                        join item in dbContext.BaseItems.AsNoTracking() on userData.ItemId equals item.Id
                        where userData.UserId.Equals(userId)
                              && userData.PlaybackPositionTicks > 0
                              && !userData.Played
                        orderby userData.LastPlayedDate descending
                        select new
                        {
                            item.Id,
                            item.Name,
                            item.SeriesName,
                            item.ParentIndexNumber,
                            item.IndexNumber,
                            item.RunTimeTicks,
                            userData.PlaybackPositionTicks,
                            userData.LastPlayedDate
                        })
                    // The UserData table stores multiple rows for the same item keyed by CustomDataKey.
                    // Pull the most recent candidates and dedupe by ItemId before projecting to UI.
                    .Take(100)
                    .ToListAsync()
                    .ConfigureAwait(false);
                var continueWatchingRows = continueWatchingCandidates
                    .GroupBy(row => row.Id)
                    .Select(group => group
                        .OrderByDescending(row => row.LastPlayedDate ?? DateTime.MinValue)
                        .ThenByDescending(row => row.PlaybackPositionTicks)
                        .First())
                    .OrderByDescending(row => row.LastPlayedDate ?? DateTime.MinValue)
                    .Take(5)
                    .ToList();

                var hasPreviousPeriod = previousPeriod is not null;
                var currentTicks = currentStats?.TotalValidatedTicks ?? 0;
                var previousTicks = hasPreviousPeriod
                    ? previousStats?.TotalValidatedTicks ?? 0
                    : currentTicks;
                var currentMovies = await CountCompletedItemsAsync(dbContext, userId, currentPeriod, nameof(Movie)).ConfigureAwait(false);
                var previousMovies = hasPreviousPeriod
                    ? await CountCompletedItemsAsync(dbContext, userId, previousPeriod!.Value, nameof(Movie)).ConfigureAwait(false)
                    : currentMovies;
                var currentEpisodes = await CountCompletedItemsAsync(dbContext, userId, currentPeriod, nameof(Episode)).ConfigureAwait(false);
                var previousEpisodes = hasPreviousPeriod
                    ? await CountCompletedItemsAsync(dbContext, userId, previousPeriod!.Value, nameof(Episode)).ConfigureAwait(false)
                    : currentEpisodes;
                var watchChangePercent = hasPreviousPeriod ? CalculateChangePercent(currentTicks, previousTicks) : 0D;
                var engagementPercentile = hasPreviousPeriod ? ComputeEngagementPercentile(currentTicks, previousTicks) : currentTicks > 0 ? 50 : 0;
                var summary = new PersonalInsightsSummaryResult
                {
                    TotalWatchHours = currentTicks / (double)TimeSpan.TicksPerHour,
                    WatchTimeChangePercent = watchChangePercent,
                    MoviesWatched = currentMovies,
                    MoviesDelta = currentMovies - previousMovies,
                    EpisodesWatched = currentEpisodes,
                    EpisodesDelta = currentEpisodes - previousEpisodes,
                    EngagementPercentile = engagementPercentile,
                    EngagementScore = engagementPercentile >= 75 ? "High" : engagementPercentile >= 40 ? "Medium" : "Low"
                };

                var hourlyDistribution = Enumerable.Range(0, 24)
                    .Select(hour =>
                    {
                        var ticks = hourlyRows
                            .Where(row => row.Hour == hour)
                            .Select(row => row.TotalValidatedTicks)
                            .FirstOrDefault();
                        return new PersonalInsightsHourlyDistributionResult
                        {
                            Hour = hour,
                            Minutes = ticks / (double)TimeSpan.TicksPerMinute
                        };
                    })
                    .ToList();
                var peakHour = hourlyDistribution
                    .OrderByDescending(distribution => distribution.Minutes)
                    .ThenBy(distribution => distribution.Hour)
                    .First()
                    .Hour;

                var genreResults = topGenres
                    .Select(genre => new PersonalInsightsGenreResult
                    {
                        Name = genre.GenreId,
                        Minutes = genre.TotalValidatedTicks / (double)TimeSpan.TicksPerMinute,
                        Percentage = currentTicks > 0 ? (genre.TotalValidatedTicks * 100D) / currentTicks : 0D
                    })
                    .ToList();

                var result = new PersonalInsightsResult
                {
                    Summary = summary,
                    PeakViewing = new PersonalInsightsPeakViewingResult
                    {
                        HourlyDistribution = hourlyDistribution,
                        PeakHour = peakHour,
                        Label = ResolvePeakLabel(peakHour)
                    },
                    ContinueWatching = continueWatchingRows.Select(row => new PersonalInsightsContinueWatchingResult
                    {
                        ItemId = row.Id,
                        Title = row.Name ?? string.Empty,
                        SeriesName = row.SeriesName ?? string.Empty,
                        SeasonNumber = row.ParentIndexNumber,
                        EpisodeNumber = row.IndexNumber,
                        RemainingMinutes = Math.Max(0, ((row.RunTimeTicks ?? 0) - row.PlaybackPositionTicks) / (double)TimeSpan.TicksPerMinute),
                        ImageUrl = string.Empty
                    }).ToList(),
                    Binge = new PersonalInsightsBingeResult
                    {
                        LongestStreak = bingeRows.Count == 0 ? 0 : bingeRows.Max(binge => binge.EpisodeCount),
                        RecentBinges = recentBinges.Select(binge => new PersonalInsightsRecentBingeResult
                        {
                            SeriesName = bingeSeriesNames.TryGetValue(binge.SeriesId, out var seriesName) ? seriesName : string.Empty,
                            EpisodeCount = binge.EpisodeCount
                        }).ToList()
                    },
                    Genres = genreResults,
                    InsightText = BuildInsightText(genreResults, currentPeriod.InsightPeriodLabel)
                };

                return result;
            }
        }

        private static Task<int> CountCompletedItemsAsync(
            JellyfinDbContext dbContext,
            Guid userId,
            PeriodDescriptor period,
            string itemType)
        {
            var itemTypes = ResolveItemTypeAliases(itemType);
            var completedQuery = from userData in dbContext.UserData.AsNoTracking()
                                 join item in dbContext.BaseItems.AsNoTracking() on userData.ItemId equals item.Id
                                 where userData.UserId.Equals(userId)
                                       && userData.Played
                                       && itemTypes.Contains(item.Type)
                                 select new
                                 {
                                     userData.ItemId,
                                     userData.LastPlayedDate
                                 };

            if (period.PeriodType != PeriodType.AllTime)
            {
                completedQuery = completedQuery.Where(row =>
                    row.LastPlayedDate.HasValue
                    && row.LastPlayedDate.Value >= period.PeriodStartUtc
                    && row.LastPlayedDate.Value < period.PeriodEndUtc);
            }

            return completedQuery
                .Select(row => row.ItemId)
                .Distinct()
                .CountAsync();
        }

        private static string[] ResolveItemTypeAliases(string itemType)
            => itemType switch
            {
                nameof(Movie) => [nameof(Movie), typeof(Movie).FullName ?? nameof(Movie)],
                nameof(Episode) => [nameof(Episode), typeof(Episode).FullName ?? nameof(Episode)],
                _ => [itemType]
            };

        private static double CalculateChangePercent(long currentValue, long previousValue)
        {
            if (previousValue <= 0)
            {
                return currentValue > 0 ? 100D : 0D;
            }

            return ((currentValue - previousValue) * 100D) / previousValue;
        }

        private static int ComputeEngagementPercentile(long currentTicks, long previousTicks)
        {
            if (currentTicks <= 0)
            {
                return 0;
            }

            if (previousTicks <= 0)
            {
                return 75;
            }

            var deltaPercent = CalculateChangePercent(currentTicks, previousTicks);
            var percentile = 50 + (deltaPercent / 2D);
            return (int)Math.Round(Math.Clamp(percentile, 1D, 99D), MidpointRounding.AwayFromZero);
        }

        private static string ResolvePeakLabel(int hour)
        {
            if (hour < 5)
            {
                return "Night Owl";
            }

            if (hour < 12)
            {
                return "Early Bird";
            }

            if (hour < 18)
            {
                return "Daytime Viewer";
            }

            return "Prime Time Watcher";
        }

        private static string BuildInsightText(System.Collections.Generic.IReadOnlyCollection<PersonalInsightsGenreResult> genres, string periodLabel)
        {
            var topGenre = genres
                .OrderByDescending(genre => genre.Percentage)
                .FirstOrDefault();
            if (topGenre is null || topGenre.Percentage <= 0)
            {
                return "Keep watching to unlock personalized insights.";
            }

            var percentage = (int)Math.Round(topGenre.Percentage, MidpointRounding.AwayFromZero);
            return $"You've spent {percentage}% of your time watching {topGenre.Name} {periodLabel}.";
        }

        private static PeriodDescriptor ResolveCurrentPeriod(PersonalInsightsPeriodType periodType, DateTime nowUtc)
        {
            var utcNow = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
            return periodType switch
            {
                PersonalInsightsPeriodType.Month => CreatePeriodDescriptor(
                    PeriodType.Month,
                    new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                    intervalMonths: 1,
                    insightLabel: "this month"),
                PersonalInsightsPeriodType.Year => CreatePeriodDescriptor(
                    PeriodType.Year,
                    new DateTime(utcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    intervalYears: 1,
                    insightLabel: "this year"),
                PersonalInsightsPeriodType.AllTime => new PeriodDescriptor(
                    PeriodType.AllTime,
                    "ALL",
                    DateTime.UnixEpoch,
                    DateTime.MaxValue,
                    "overall"),
                _ => throw new ArgumentOutOfRangeException(nameof(periodType))
            };
        }

        private static PeriodDescriptor? ResolvePreviousPeriod(PersonalInsightsPeriodType periodType, DateTime nowUtc)
        {
            var utcNow = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
            return periodType switch
            {
                PersonalInsightsPeriodType.Month => CreatePeriodDescriptor(
                    PeriodType.Month,
                    new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1),
                    intervalMonths: 1,
                    insightLabel: "this month"),
                PersonalInsightsPeriodType.Year => CreatePeriodDescriptor(
                    PeriodType.Year,
                    new DateTime(utcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddYears(-1),
                    intervalYears: 1,
                    insightLabel: "this year"),
                PersonalInsightsPeriodType.AllTime => null,
                _ => throw new ArgumentOutOfRangeException(nameof(periodType))
            };
        }

        private static PeriodDescriptor CreatePeriodDescriptor(
            PeriodType periodType,
            DateTime startUtc,
            int intervalMonths = 0,
            int intervalYears = 0,
            string insightLabel = "")
        {
            var endUtc = intervalMonths > 0
                ? startUtc.AddMonths(intervalMonths)
                : startUtc.AddYears(intervalYears);
            var key = periodType == PeriodType.Month
                ? startUtc.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                : startUtc.ToString("yyyy", CultureInfo.InvariantCulture);
            return new PeriodDescriptor(periodType, key, startUtc, endUtc, insightLabel);
        }

        private readonly record struct PeriodDescriptor(
            PeriodType PeriodType,
            string PeriodKey,
            DateTime PeriodStartUtc,
            DateTime PeriodEndUtc,
            string InsightPeriodLabel);
    }
}
