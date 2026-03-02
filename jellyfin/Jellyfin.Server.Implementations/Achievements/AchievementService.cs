using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Extensions;
using MediaBrowser.Controller.Achievements;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Server.Implementations.Achievements
{
    /// <inheritdoc />
    public class AchievementService : IAchievementService
    {
        private const int _defaultTake = 200;
        private const int _maxTake = 500;
        private const string _allTimePeriodKey = "ALL";

        private static readonly string[] _majorGenreKeys =
        [
            "action",
            "drama",
            "comedy",
            "thriller",
            "scifi",
            "romance",
            "horror",
            "mystery",
            "animation",
            "fantasy"
        ];

        private static readonly string[] _homeCountryHints =
        [
            "india",
            "united states",
            "usa",
            "us"
        ];

        private static readonly TimeZoneInfo _insightsTimeZone = ResolveInsightsTimeZone();
        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="AchievementService"/> class.
        /// </summary>
        /// <param name="dbProvider">Database provider.</param>
        public AchievementService(IDbContextFactory<JellyfinDbContext> dbProvider)
        {
            _dbProvider = dbProvider;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AchievementDefinitionInfo>> GetDefinitions(bool includeSeasonal)
        {
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var query = dbContext.AchievementDefinitions.AsNoTracking();
                if (!includeSeasonal)
                {
                    query = query.Where(definition => !definition.IsSeasonal);
                }

                var rows = await query
                    .OrderBy(definition => definition.IsSeasonal)
                    .ThenBy(definition => definition.Title)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return rows.Select(ToDefinitionInfo).ToList();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<UserAchievementInfo>> GetHistory(Guid userId, int take)
        {
            if (userId.IsEmpty())
            {
                throw new ArgumentException("User id cannot be empty.", nameof(userId));
            }

            var normalizedTake = take <= 0 ? _defaultTake : Math.Min(take, _maxTake);
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var rows = await (
                    from unlock in dbContext.UserAchievements.AsNoTracking()
                    join definition in dbContext.AchievementDefinitions.AsNoTracking()
                        on unlock.AchievementId equals definition.Id
                    where unlock.UserId.Equals(userId)
                    orderby unlock.UnlockedAtUtc descending
                    select new UserAchievementInfo
                    {
                        Id = definition.Id,
                        Title = definition.Title,
                        Description = definition.Description,
                        ImageEmoji = definition.ImageEmoji,
                        Rarity = definition.Rarity,
                        Xp = definition.Xp,
                        Coins = definition.Coins,
                        UnlockedAt = unlock.UnlockedAtUtc,
                        IsSeasonal = definition.IsSeasonal,
                        SeasonType = definition.SeasonType,
                        SeasonYear = unlock.SeasonYear
                    })
                    .Take(normalizedTake)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return rows;
            }
        }

        /// <inheritdoc />
        public async Task<AchievementUnlockResult> Unlock(Guid userId, string achievementId)
        {
            if (userId.IsEmpty())
            {
                throw new ArgumentException("User id cannot be empty.", nameof(userId));
            }

            var normalizedAchievementId = NormalizeAchievementId(achievementId);
            if (string.IsNullOrEmpty(normalizedAchievementId))
            {
                throw new ArgumentException("Achievement id is required.", nameof(achievementId));
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var definition = await dbContext.AchievementDefinitions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(row => row.Id == normalizedAchievementId)
                    .ConfigureAwait(false)
                    ?? throw new AchievementNotFoundException("Achievement not found.");

                var nowUtc = DateTime.UtcNow;
                var seasonYear = GetSeasonYear(definition, nowUtc);

                var existingUnlockedAt = await dbContext.UserAchievements
                    .AsNoTracking()
                    .Where(row => row.UserId.Equals(userId)
                        && row.AchievementId == normalizedAchievementId
                        && row.SeasonYear == seasonYear)
                    .Select(row => new
                    {
                        row.UnlockedAtUtc,
                        row.SeasonYear
                    })
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (existingUnlockedAt is not null)
                {
                    return new AchievementUnlockResult
                    {
                        Unlocked = false,
                        Achievement = ToUserAchievementInfo(definition, existingUnlockedAt.UnlockedAtUtc, existingUnlockedAt.SeasonYear)
                    };
                }

                var unlockTimestampUtc = nowUtc;
                dbContext.UserAchievements.Add(new UserAchievement
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    AchievementId = normalizedAchievementId,
                    UnlockedAtUtc = unlockTimestampUtc,
                    SeasonYear = seasonYear
                });

                try
                {
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                }
                catch (DbUpdateException)
                {
                    var duplicateUnlockedAt = await dbContext.UserAchievements
                        .AsNoTracking()
                        .Where(row => row.UserId.Equals(userId)
                            && row.AchievementId == normalizedAchievementId
                            && row.SeasonYear == seasonYear)
                        .Select(row => new
                        {
                            row.UnlockedAtUtc,
                            row.SeasonYear
                        })
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);

                    if (duplicateUnlockedAt is null)
                    {
                        throw;
                    }

                    return new AchievementUnlockResult
                    {
                        Unlocked = false,
                        Achievement = ToUserAchievementInfo(definition, duplicateUnlockedAt.UnlockedAtUtc, duplicateUnlockedAt.SeasonYear)
                    };
                }

                return new AchievementUnlockResult
                {
                    Unlocked = true,
                    Achievement = ToUserAchievementInfo(definition, unlockTimestampUtc, seasonYear)
                };
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<UserAchievementInfo>> Sync(Guid userId)
        {
            if (userId.IsEmpty())
            {
                throw new ArgumentException("User id cannot be empty.", nameof(userId));
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var nowUtc = DateTime.UtcNow;
                var currentSeasonYear = GetCurrentSeasonYear(nowUtc);
                var definitions = await dbContext.AchievementDefinitions
                    .AsNoTracking()
                    .ToDictionaryAsync(definition => definition.Id, StringComparer.Ordinal)
                    .ConfigureAwait(false);

                if (definitions.Count == 0)
                {
                    return [];
                }

                var existingUnlockRows = await dbContext.UserAchievements
                    .AsNoTracking()
                    .Where(row => row.UserId.Equals(userId))
                    .Select(row => new
                    {
                        row.AchievementId,
                        row.SeasonYear
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

                var unlockedIds = new HashSet<string>(StringComparer.Ordinal);
                var lifetimeUnlockedIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var unlockRow in existingUnlockRows)
                {
                    lifetimeUnlockedIds.Add(unlockRow.AchievementId);

                    if (!definitions.TryGetValue(unlockRow.AchievementId, out var definition))
                    {
                        continue;
                    }

                    if (!definition.IsSeasonal && !unlockRow.SeasonYear.HasValue)
                    {
                        unlockedIds.Add(unlockRow.AchievementId);
                        continue;
                    }

                    if (definition.IsSeasonal && unlockRow.SeasonYear == currentSeasonYear)
                    {
                        unlockedIds.Add(unlockRow.AchievementId);
                    }
                }

                var metrics = await BuildMetricsAsync(dbContext, userId).ConfigureAwait(false);
                var pendingIds = EvaluatePendingUnlocks(definitions, metrics, unlockedIds, lifetimeUnlockedIds);
                if (pendingIds.Count == 0)
                {
                    return [];
                }

                var rows = pendingIds
                    .Select((achievementId, index) => new UserAchievement
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        AchievementId = achievementId,
                        UnlockedAtUtc = nowUtc.AddMilliseconds(index),
                        SeasonYear = GetSeasonYear(definitions[achievementId], nowUtc)
                    })
                    .ToList();

                dbContext.UserAchievements.AddRange(rows);

                try
                {
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                    return rows.Select(row => ToUserAchievementInfo(definitions[row.AchievementId], row.UnlockedAtUtc, row.SeasonYear)).ToList();
                }
                catch (DbUpdateException)
                {
                    var unlockedRows = new List<UserAchievementInfo>();
                    foreach (var achievementId in pendingIds)
                    {
                        try
                        {
                            var result = await Unlock(userId, achievementId).ConfigureAwait(false);
                            if (result.Unlocked)
                            {
                                unlockedRows.Add(result.Achievement);
                            }
                        }
                        catch (AchievementNotFoundException)
                        {
                        }
                    }

                    return unlockedRows;
                }
            }
        }

        private static List<string> EvaluatePendingUnlocks(
            IReadOnlyDictionary<string, AchievementDefinition> definitions,
            Metrics metrics,
            HashSet<string> unlockedIds,
            HashSet<string> lifetimeUnlockedIds)
        {
            var pendingIds = new List<string>();
            var pendingCoins = 0;
            var loops = 0;

            while (loops++ < definitions.Count)
            {
                var before = unlockedIds.Count;
                var totalCoins = metrics.EarnedAchievementCoins + pendingCoins;
                var unlockedCount = lifetimeUnlockedIds.Count;
                int Genre(string key) => metrics.GenreCounts.TryGetValue(key, out var count) ? count : 0;

                void TryUnlock(string id, bool condition)
                {
                    if (!condition || !definitions.ContainsKey(id) || !unlockedIds.Add(id))
                    {
                        return;
                    }

                    pendingIds.Add(id);
                    pendingCoins += definitions[id].Coins;
                    lifetimeUnlockedIds.Add(id);
                }

                // ONBOARDING
                TryUnlock("first-stream", metrics.HasAnyWatch);
                TryUnlock("episode-one", metrics.CompletedEpisodes >= 1);
                TryUnlock("feature-film", metrics.CompletedMovies >= 1);
                TryUnlock("first-request", metrics.TotalRequests >= 1);
                TryUnlock("request-approved-first", metrics.ApprovedOrCompletedRequests >= 1);
                TryUnlock("weekend-viewer", metrics.HasWeekendWatch);
                TryUnlock("night-owl", metrics.HasNightWatch);
                TryUnlock("early-bird", metrics.HasEarlyBirdWatch);
                TryUnlock("double-feature", metrics.MaxTitlesInSingleDay >= 2);
                TryUnlock("platform-explorer", metrics.DistinctPlayedTitles >= 10);

                // EPISODE PROGRESSION
                TryUnlock("five-episodes", metrics.CompletedEpisodes >= 5);
                TryUnlock("ten-episodes", metrics.CompletedEpisodes >= 10);
                TryUnlock("twenty-episodes", metrics.CompletedEpisodes >= 20);
                TryUnlock("fifty-episodes", metrics.CompletedEpisodes >= 50);
                TryUnlock("hundred-episodes", metrics.CompletedEpisodes >= 100);
                TryUnlock("binge-session", metrics.MaxBingeEpisodeCount >= 3 || metrics.BingeSessionCount >= 1);
                TryUnlock("mega-binge", metrics.MaxBingeEpisodeCount >= 10 || metrics.MaxEpisodesInSingleDay >= 10);
                TryUnlock("season-finisher", metrics.HasSeasonFinisher);
                TryUnlock("trilogy-night", metrics.MaxMoviesInSingleDay >= 3);
                TryUnlock("cliffhanger-survivor", metrics.HasCliffhangerSurvivor);
                TryUnlock("midnight-marathon", metrics.MidnightEpisodeCount >= 2);
                TryUnlock("weekend-marathon", metrics.WeekendEpisodeCount >= 8);
                TryUnlock("back-to-back", metrics.HasConsecutiveEpisodeDays);
                TryUnlock("one-sitting", metrics.HasOneSittingSeasonFinish);
                TryUnlock("rewatcher", metrics.HasSeriesRewatch);

                // MOVIES
                TryUnlock("movie-buff", metrics.CompletedMovies >= 5);
                TryUnlock("cinema-lover", metrics.CompletedMovies >= 15);
                TryUnlock("film-collector", metrics.CompletedMovies >= 50);
                TryUnlock("classic-viewer", metrics.HasClassicMovieWatch);
                TryUnlock("new-release", metrics.HasNewReleaseWatch);
                TryUnlock("long-haul", metrics.HasLongHaulMovieWatch);
                TryUnlock("short-story", metrics.HasShortStoryMovieWatch);
                TryUnlock("double-movie-night", metrics.MaxMoviesInSingleDay >= 2);
                TryUnlock("international-film", metrics.InternationalMovieCount >= 1);
                TryUnlock("documentary-dive", Genre("documentary") >= 5);

                // GENRE
                TryUnlock("action-fan", Genre("action") >= 1);
                TryUnlock("drama-enthusiast", Genre("drama") >= 1);
                TryUnlock("comedy-club", Genre("comedy") >= 1);
                TryUnlock("thriller-seeker", Genre("thriller") >= 1);
                TryUnlock("scifi-explorer", Genre("scifi") >= 1);
                TryUnlock("romance-viewer", Genre("romance") >= 1);
                TryUnlock("horror-night", Genre("horror") >= 1 && metrics.HasNightWatch);
                TryUnlock("mystery-mind", Genre("mystery") >= 1);
                TryUnlock("animation-watcher", Genre("animation") >= 1);
                TryUnlock("fantasy-realm", Genre("fantasy") >= 1);
                TryUnlock("crime-analyst", Genre("crime") >= 5);
                TryUnlock("history-buff", Genre("history") >= 5);
                TryUnlock("biography-viewer", Genre("biography") >= 3);
                TryUnlock("family-time", Genre("family") >= 1);
                TryUnlock("genre-loyalist", metrics.MaxGenreCount >= 10);
                TryUnlock("genre-explorer", metrics.UniqueGenreCount >= 8);
                TryUnlock("balanced-viewer", metrics.GenresWithAtLeastTwoTitles >= 5);
                TryUnlock("global-explorer", metrics.UniqueCountryCount >= 10);
                TryUnlock("award-winner", metrics.HasAwardWinnerWatch);
                TryUnlock("critics-choice", metrics.HighlyRatedTitles >= 5);

                // REQUEST SYSTEM
                TryUnlock("request-pioneer", metrics.TotalRequests >= 3);
                TryUnlock("request-regular", metrics.TotalRequests >= 10);
                TryUnlock("request-strategist", metrics.RequestGenreDiversity >= 3);
                TryUnlock("popular-choice", metrics.PopularRequestCount >= 1);
                TryUnlock("curator", metrics.RequestActiveSpanDays >= 30 && metrics.TotalRequests >= 2);
                TryUnlock("content-contributor", metrics.ApprovedOrCompletedRequests >= 10);
                TryUnlock("trend-starter", metrics.WidelyWatchedRequestCount >= 1);
                TryUnlock("community-driver", metrics.PopularRequestCount >= 3);
                TryUnlock("smart-spender", metrics.TotalRequests >= 1);
                TryUnlock("coin-collector", totalCoins >= 100);
                TryUnlock("coin-hoarder", totalCoins >= 500);
                TryUnlock("high-roller", metrics.ApprovedOrCompletedRequests >= 15);
                TryUnlock("loyal-redeemer", metrics.LongestDailyStreak >= 30);
                TryUnlock("boost-master", metrics.TotalRequests >= 20);
                TryUnlock("strategic-planner", metrics.TotalRequests >= 10 && metrics.RequestSuccessRatioPercent >= 70);

                // SUBSCRIPTION
                TryUnlock("loyal-member", metrics.LongestContinuousSubscriptionMonths >= 3);
                TryUnlock("dedicated-viewer", metrics.LongestContinuousSubscriptionMonths >= 6);
                TryUnlock("year-one", metrics.LongestContinuousSubscriptionMonths >= 12);
                TryUnlock("anniversary", metrics.LongestContinuousSubscriptionMonths >= 24);
                TryUnlock("comeback", metrics.HasSubscriptionComeback);
                TryUnlock("continuous-supporter", metrics.LongestContinuousSubscriptionMonths >= 18);
                TryUnlock("early-renewal", metrics.HasEarlyRenewal);
                TryUnlock("premium-supporter", metrics.LongestContinuousSubscriptionMonths >= 12 && metrics.OnTimeRenewalCount >= 1);
                TryUnlock("stability", metrics.OnTimeRenewalCount >= 6);
                TryUnlock("founding-member", metrics.IsFoundingMember);

                // TIME
                TryUnlock("ten-hours", metrics.TotalWatchHours >= 10D);
                TryUnlock("fifty-hours", metrics.TotalWatchHours >= 50D);
                TryUnlock("hundred-hours", metrics.TotalWatchHours >= 100D);
                TryUnlock("two-fifty-hours", metrics.TotalWatchHours >= 250D);
                TryUnlock("five-hundred-hours", metrics.TotalWatchHours >= 500D);
                TryUnlock("daily-viewer", metrics.LongestDailyStreak >= 7);
                TryUnlock("weekly-habit", metrics.LongestWeeklyStreak >= 8);
                TryUnlock("monthly-active", metrics.LongestMonthlyStreak >= 12);
                TryUnlock("comeback-king", metrics.HasComebackAfterInactivity);
                TryUnlock("prime-time", metrics.PrimeTimeSessionCount >= 20);

                // PRESTIGE
                TryUnlock("completionist", metrics.DistinctPlayedTitles >= 100);
                TryUnlock("master-viewer", metrics.DistinctPlayedTitles >= 250);
                TryUnlock("elite-curator", metrics.ApprovedOrCompletedRequests >= 25);
                TryUnlock("ultimate-binger", metrics.BingeSessionCount >= 20);
                TryUnlock("cinematic-scholar", metrics.MajorGenresCovered >= _majorGenreKeys.Length);
                TryUnlock("genre-master", metrics.GenresWithAtLeastFiveTitles >= 10);
                TryUnlock("global-cinema", metrics.InternationalMovieCount >= 100);
                TryUnlock("platform-veteran", metrics.IsPlatformVeteran);
                TryUnlock("legend", unlockedCount >= 90);
                TryUnlock("immortal-viewer", unlockedCount >= 99);

                if (unlockedIds.Count == before)
                {
                    break;
                }
            }

            return pendingIds;
        }

        private static async Task<Metrics> BuildMetricsAsync(JellyfinDbContext dbContext, Guid userId)
        {
            var nowUtc = DateTime.UtcNow;
            var metrics = new Metrics();
            metrics.EarnedAchievementCoins = await (
                    from unlock in dbContext.UserAchievements.AsNoTracking()
                    join definition in dbContext.AchievementDefinitions.AsNoTracking()
                        on unlock.AchievementId equals definition.Id
                    where unlock.UserId.Equals(userId)
                    select (int?)definition.Coins)
                .SumAsync()
                .ConfigureAwait(false) ?? 0;

            var allTime = await dbContext.UserPeriodStats
                .AsNoTracking()
                .FirstOrDefaultAsync(stats => stats.UserId.Equals(userId)
                    && stats.PeriodType == PeriodType.AllTime
                    && stats.PeriodKey == _allTimePeriodKey)
                .ConfigureAwait(false);

            var playedRaw = await (
                    from userData in dbContext.UserData.AsNoTracking()
                    join item in dbContext.BaseItems.AsNoTracking() on userData.ItemId equals item.Id
                    where userData.UserId.Equals(userId) && userData.Played
                    select new
                    {
                        userData.ItemId,
                        userData.LastPlayedDate,
                        userData.PlayCount,
                        item.Type,
                        item.RunTimeTicks,
                        item.DateCreated,
                        item.ProductionYear,
                        item.Name,
                        item.OriginalTitle,
                        item.Genres,
                        item.ProductionLocations,
                        item.CommunityRating,
                        item.CriticRating,
                        item.SeriesId,
                        item.ParentIndexNumber,
                        item.IndexNumber
                    })
                .ToListAsync()
                .ConfigureAwait(false);

            var played = playedRaw
                .GroupBy(row => row.ItemId)
                .Select(group => group
                    .OrderByDescending(row => row.LastPlayedDate ?? DateTime.MinValue)
                    .ThenByDescending(row => row.PlayCount)
                    .First())
                .ToList();

            var sessions = await (
                    from session in dbContext.UserWatchSessions.AsNoTracking()
                    join item in dbContext.BaseItems.AsNoTracking() on session.ItemId equals item.Id
                    where session.UserId.Equals(userId)
                          && session.IsValidSession
                          && session.ValidatedTicks > 0
                    select new
                    {
                        session.ItemId,
                        session.StartTimeUtc,
                        session.EndTimeUtc,
                        session.ValidatedTicks,
                        item.Type,
                        item.SeriesId,
                        item.ParentIndexNumber,
                        item.IndexNumber
                    })
                .ToListAsync()
                .ConfigureAwait(false);

            var bingeRows = await dbContext.UserBingeSessions
                .AsNoTracking()
                .Where(row => row.UserId.Equals(userId))
                .Select(row => row.EpisodeCount)
                .ToListAsync()
                .ConfigureAwait(false);

            var requests = await dbContext.ContentRequests
                .AsNoTracking()
                .Where(row => row.UserId.Equals(userId))
                .Select(row => new
                {
                    row.RequestedAt,
                    row.Status,
                    row.JellyfinItemId
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var linkedItemIds = requests
                .Where(row => row.JellyfinItemId.HasValue)
                .Select(row => row.JellyfinItemId!.Value)
                .Distinct()
                .ToArray();

            var itemWatchers = new Dictionary<Guid, int>();
            var requestItemGenres = new Dictionary<Guid, string>();
            if (linkedItemIds.Length > 0)
            {
                var viewerRows = await dbContext.UserData
                    .AsNoTracking()
                    .Where(row => row.Played && linkedItemIds.Contains(row.ItemId))
                    .Select(row => new { row.ItemId, row.UserId })
                    .ToListAsync()
                    .ConfigureAwait(false);

                itemWatchers = viewerRows
                    .GroupBy(row => row.ItemId)
                    .ToDictionary(group => group.Key, group => group.Select(row => row.UserId).Distinct().Count());

                requestItemGenres = await dbContext.BaseItems
                    .AsNoTracking()
                    .Where(item => linkedItemIds.Contains(item.Id))
                    .Select(item => new { item.Id, item.Genres })
                    .ToDictionaryAsync(row => row.Id, row => row.Genres ?? string.Empty)
                    .ConfigureAwait(false);
            }

            var episodePlayed = played.Where(row => IsEpisodeType(row.Type)).ToList();
            var watchedSeriesIds = episodePlayed
                .Where(row => row.SeriesId.HasValue)
                .Select(row => row.SeriesId!.Value)
                .Distinct()
                .ToArray();

            var episodeCatalog = watchedSeriesIds.Length == 0
                ? []
                : await dbContext.BaseItems
                    .AsNoTracking()
                    .Where(item => item.SeriesId.HasValue
                        && watchedSeriesIds.Contains(item.SeriesId.Value)
                        && item.ParentIndexNumber.HasValue
                        && item.IndexNumber.HasValue)
                    .Select(item => new
                    {
                        item.Type,
                        SeriesId = item.SeriesId!.Value,
                        Season = item.ParentIndexNumber!.Value,
                        Episode = item.IndexNumber!.Value
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

            var (subLongestMonths, subComeback, earlyRenewal, onTimeRenewals, firstRedeemedAtUtc) =
                await ResolveSubscriptionStatsAsync(dbContext, userId).ConfigureAwait(false);

            metrics.HasAnyWatch = played.Count > 0 || sessions.Count > 0;
            metrics.DistinctPlayedTitles = played.Count;
            metrics.CompletedMovies = Math.Max(allTime?.CompletedMovies ?? 0, played.Count(row => IsMovieType(row.Type)));
            metrics.CompletedEpisodes = Math.Max(allTime?.CompletedEpisodes ?? 0, episodePlayed.Count);

            var totalWatchTicks = allTime?.TotalValidatedTicks ?? sessions.Sum(row => row.ValidatedTicks);
            metrics.TotalWatchHours = totalWatchTicks / (double)TimeSpan.TicksPerHour;
            metrics.BingeSessionCount = Math.Max(allTime?.BingeSessions ?? 0, bingeRows.Count);
            metrics.MaxBingeEpisodeCount = bingeRows.Count == 0 ? 0 : bingeRows.Max();

            var activityDates = new HashSet<DateOnly>();
            var episodeDates = new HashSet<DateOnly>();
            var dayTitles = new Dictionary<DateOnly, int>();
            var dayMovies = new Dictionary<DateOnly, int>();
            var dayEpisodes = new Dictionary<DateOnly, int>();
            var countries = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in played)
            {
                if (!row.LastPlayedDate.HasValue)
                {
                    continue;
                }

                var localDateTime = ToInsightsLocalTime(row.LastPlayedDate.Value);
                var localDate = DateOnly.FromDateTime(localDateTime);
                activityDates.Add(localDate);
                Inc(dayTitles, localDate);

                if (localDateTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    metrics.HasWeekendWatch = true;
                }

                if (localDateTime.Hour < 5)
                {
                    metrics.HasNightWatch = true;
                }

                if (localDateTime.Hour >= 5 && localDateTime.Hour < 8)
                {
                    metrics.HasEarlyBirdWatch = true;
                }

                if ((row.CommunityRating ?? 0) >= 7.5F || (row.CriticRating ?? 0) >= 7.5F)
                {
                    metrics.HighlyRatedTitles++;
                }

                if ((row.CommunityRating ?? 0) >= 8.8F || (row.CriticRating ?? 0) >= 8.8F)
                {
                    metrics.HasAwardWinnerWatch = true;
                }

                if (IsMovieType(row.Type))
                {
                    Inc(dayMovies, localDate);

                    if (row.ProductionYear.HasValue && row.ProductionYear.Value <= 1990)
                    {
                        metrics.HasClassicMovieWatch = true;
                    }

                    if ((row.RunTimeTicks ?? 0) > TimeSpan.FromHours(3).Ticks)
                    {
                        metrics.HasLongHaulMovieWatch = true;
                    }

                    if ((row.RunTimeTicks ?? 0) > 0 && (row.RunTimeTicks ?? 0) < TimeSpan.FromMinutes(90).Ticks)
                    {
                        metrics.HasShortStoryMovieWatch = true;
                    }

                    if (row.DateCreated.HasValue && row.LastPlayedDate.Value <= row.DateCreated.Value.AddDays(7))
                    {
                        metrics.HasNewReleaseWatch = true;
                    }
                }

                if (IsEpisodeType(row.Type))
                {
                    Inc(dayEpisodes, localDate);
                    episodeDates.Add(localDate);
                }

                var genreSet = SplitValues(row.Genres)
                    .Select(NormalizeGenreKey)
                    .Where(genre => !string.IsNullOrEmpty(genre))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                foreach (var genre in genreSet)
                {
                    Inc(metrics.GenreCounts, genre);
                }

                var countrySet = SplitValues(row.ProductionLocations)
                    .Select(NormalizeCountryKey)
                    .Where(country => !string.IsNullOrEmpty(country))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                foreach (var country in countrySet)
                {
                    Inc(countries, country);
                }

                if (IsMovieType(row.Type) && IsInternationalTitle(countrySet, row.Name ?? string.Empty, row.OriginalTitle ?? string.Empty))
                {
                    metrics.InternationalMovieCount++;
                }
            }

            metrics.MaxTitlesInSingleDay = dayTitles.Count == 0 ? 0 : dayTitles.Values.Max();
            metrics.MaxMoviesInSingleDay = dayMovies.Count == 0 ? 0 : dayMovies.Values.Max();
            metrics.MaxEpisodesInSingleDay = dayEpisodes.Count == 0 ? 0 : dayEpisodes.Values.Max();
            metrics.WeekendEpisodeCount = dayEpisodes.Where(pair => pair.Key.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).Sum(pair => pair.Value);
            metrics.MidnightEpisodeCount = episodePlayed.Count(row => row.LastPlayedDate.HasValue && ToInsightsLocalTime(row.LastPlayedDate.Value).Hour < 5);

            var episodeSessions = sessions
                .Where(row => IsEpisodeType(row.Type))
                .OrderBy(row => row.StartTimeUtc)
                .ToList();
            for (var index = 0; index + 1 < episodeSessions.Count; index++)
            {
                var current = episodeSessions[index];
                var next = episodeSessions[index + 1];
                if (!current.SeriesId.HasValue
                    || !next.SeriesId.HasValue
                    || !current.ParentIndexNumber.HasValue
                    || !next.ParentIndexNumber.HasValue
                    || !current.IndexNumber.HasValue
                    || !next.IndexNumber.HasValue
                    || !current.SeriesId.Value.Equals(next.SeriesId.Value)
                    || current.ParentIndexNumber.Value != next.ParentIndexNumber.Value
                    || current.IndexNumber.Value + 1 != next.IndexNumber.Value)
                {
                    continue;
                }

                var currentEnd = current.EndTimeUtc ?? current.StartTimeUtc.AddTicks(Math.Max(1, current.ValidatedTicks));
                var gap = next.StartTimeUtc - currentEnd;
                if (gap >= TimeSpan.Zero && gap <= TimeSpan.FromMinutes(5))
                {
                    metrics.HasCliffhangerSurvivor = true;
                    break;
                }
            }

            var watchedBySeason = new Dictionary<(Guid SeriesId, int Season), HashSet<int>>();
            var watchedDatesBySeason = new Dictionary<(Guid SeriesId, int Season), HashSet<DateOnly>>();
            foreach (var row in episodePlayed)
            {
                if (!row.SeriesId.HasValue || !row.ParentIndexNumber.HasValue || !row.IndexNumber.HasValue || row.IndexNumber.Value <= 0)
                {
                    continue;
                }

                var key = (row.SeriesId.Value, row.ParentIndexNumber.Value);
                if (!watchedBySeason.TryGetValue(key, out var set))
                {
                    set = [];
                    watchedBySeason[key] = set;
                }

                set.Add(row.IndexNumber.Value);

                if (row.LastPlayedDate.HasValue)
                {
                    if (!watchedDatesBySeason.TryGetValue(key, out var dateSet))
                    {
                        dateSet = [];
                        watchedDatesBySeason[key] = dateSet;
                    }

                    dateSet.Add(DateOnly.FromDateTime(ToInsightsLocalTime(row.LastPlayedDate.Value)));
                }
            }

            var catalogBySeason = new Dictionary<(Guid SeriesId, int Season), HashSet<int>>();
            foreach (var row in episodeCatalog.Where(row => IsEpisodeType(row.Type) && row.Episode > 0))
            {
                var key = (row.SeriesId, row.Season);
                if (!catalogBySeason.TryGetValue(key, out var set))
                {
                    set = [];
                    catalogBySeason[key] = set;
                }

                set.Add(row.Episode);
            }

            foreach (var pair in watchedBySeason)
            {
                if (!catalogBySeason.TryGetValue(pair.Key, out var allEpisodes)
                    || allEpisodes.Count < 2
                    || pair.Value.Count < allEpisodes.Count
                    || !pair.Value.IsSupersetOf(allEpisodes))
                {
                    continue;
                }

                metrics.HasSeasonFinisher = true;
                if (watchedDatesBySeason.TryGetValue(pair.Key, out var dates) && dates.Count == 1)
                {
                    metrics.HasOneSittingSeasonFinish = true;
                }
            }

            metrics.HasSeriesRewatch = episodePlayed
                .Where(row => row.SeriesId.HasValue)
                .GroupBy(row => row.SeriesId!.Value)
                .Any(group => group.Count() >= 3 && group.All(row => row.PlayCount >= 2));

            metrics.PrimeTimeSessionCount = sessions.Count(row =>
            {
                var localHour = ToInsightsLocalTime(row.StartTimeUtc).Hour;
                return localHour >= 18 && localHour <= 23;
            });

            var orderedActivityDates = activityDates.OrderBy(date => date).ToList();
            var orderedEpisodeDates = episodeDates.OrderBy(date => date).ToList();

            metrics.HasConsecutiveEpisodeDays = HasConsecutive(orderedEpisodeDates, 2);
            metrics.LongestDailyStreak = LongestDaily(orderedActivityDates);
            metrics.LongestWeeklyStreak = LongestWeekly(orderedActivityDates);
            metrics.LongestMonthlyStreak = LongestMonthly(orderedActivityDates);
            metrics.HasComebackAfterInactivity = HasInactivityGap(orderedActivityDates, 14);
            metrics.UniqueCountryCount = countries.Count;
            metrics.UniqueGenreCount = metrics.GenreCounts.Count;
            metrics.MaxGenreCount = metrics.GenreCounts.Count == 0 ? 0 : metrics.GenreCounts.Values.Max();
            metrics.GenresWithAtLeastTwoTitles = metrics.GenreCounts.Values.Count(value => value >= 2);
            metrics.GenresWithAtLeastFiveTitles = metrics.GenreCounts.Values.Count(value => value >= 5);
            metrics.MajorGenresCovered = _majorGenreKeys.Count(key => metrics.GenreCounts.TryGetValue(key, out var count) && count > 0);

            metrics.TotalRequests = requests.Count;
            metrics.ApprovedOrCompletedRequests = requests.Count(row => row.Status == ContentRequestStatus.Approved || row.Status == ContentRequestStatus.Completed);
            metrics.RequestSuccessRatioPercent = metrics.TotalRequests == 0
                ? 0
                : (int)Math.Round((metrics.ApprovedOrCompletedRequests * 100D) / metrics.TotalRequests, MidpointRounding.AwayFromZero);
            metrics.RequestActiveSpanDays = requests.Count < 2
                ? 0
                : (int)Math.Floor((requests.Max(row => row.RequestedAt) - requests.Min(row => row.RequestedAt)).TotalDays);

            var requestGenreSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var request in requests)
            {
                if (!request.JellyfinItemId.HasValue)
                {
                    continue;
                }

                if (requestItemGenres.TryGetValue(request.JellyfinItemId.Value, out var genres))
                {
                    foreach (var genre in SplitValues(genres).Select(NormalizeGenreKey).Where(genre => !string.IsNullOrEmpty(genre)))
                    {
                        requestGenreSet.Add(genre);
                    }
                }

                if (itemWatchers.TryGetValue(request.JellyfinItemId.Value, out var viewers))
                {
                    if (viewers >= 5)
                    {
                        metrics.PopularRequestCount++;
                    }

                    if (viewers >= 20)
                    {
                        metrics.WidelyWatchedRequestCount++;
                    }
                }
            }

            metrics.RequestGenreDiversity = requestGenreSet.Count;
            metrics.LongestContinuousSubscriptionMonths = subLongestMonths;
            metrics.HasSubscriptionComeback = subComeback;
            metrics.HasEarlyRenewal = earlyRenewal;
            metrics.OnTimeRenewalCount = onTimeRenewals;

            var firstPlayedUtc = played.Where(row => row.LastPlayedDate.HasValue).Select(row => (DateTime?)row.LastPlayedDate!.Value).OrderBy(date => date).FirstOrDefault();
            var firstSessionUtc = sessions.Select(row => (DateTime?)row.StartTimeUtc).OrderBy(date => date).FirstOrDefault();
            var firstRequestUtc = requests.Select(row => (DateTime?)row.RequestedAt).OrderBy(date => date).FirstOrDefault();
            var firstActivityUtc = MinDate(firstPlayedUtc, firstSessionUtc, firstRequestUtc, firstRedeemedAtUtc);

            metrics.IsFoundingMember = (firstRedeemedAtUtc ?? firstActivityUtc).HasValue
                && (firstRedeemedAtUtc ?? firstActivityUtc)!.Value <= nowUtc.AddYears(-2);
            var activeMonthCount = orderedActivityDates.Select(date => (date.Year, date.Month)).Distinct().Count();
            metrics.IsPlatformVeteran = firstActivityUtc.HasValue
                && firstActivityUtc.Value <= nowUtc.AddYears(-3)
                && activeMonthCount >= 24;

            return metrics;
        }

        private static async Task<(int LongestMonths, bool HasComeback, bool HasEarlyRenewal, int OnTimeRenewals, DateTime? FirstRedeemedAtUtc)> ResolveSubscriptionStatsAsync(
            JellyfinDbContext dbContext,
            Guid userId)
        {
            var rows = await dbContext.AccessKeys
                .AsNoTracking()
                .Where(accessKey => accessKey.IsRedeemed
                    && accessKey.RedeemedByUserId.HasValue
                    && accessKey.RedeemedByUserId.Value.Equals(userId)
                    && accessKey.RedeemedAt.HasValue)
                .OrderBy(accessKey => accessKey.RedeemedAt)
                .Select(accessKey => new
                {
                    RedeemedAtUtc = DateTime.SpecifyKind(accessKey.RedeemedAt!.Value, DateTimeKind.Utc),
                    DurationMonths = Math.Max(0, accessKey.DurationMonths)
                })
                .ToListAsync()
                .ConfigureAwait(false);

            if (rows.Count == 0)
            {
                return (0, false, false, 0, null);
            }

            var hasComeback = false;
            var hasEarlyRenewal = false;
            var onTimeRenewals = 0;
            var longest = rows[0].DurationMonths;
            var currentMonths = rows[0].DurationMonths;
            var currentExpiry = rows[0].RedeemedAtUtc.AddMonths(rows[0].DurationMonths);

            for (var index = 1; index < rows.Count; index++)
            {
                var row = rows[index];
                if (row.RedeemedAtUtc <= currentExpiry.AddDays(1))
                {
                    onTimeRenewals++;
                    if (row.RedeemedAtUtc < currentExpiry.AddDays(-1))
                    {
                        hasEarlyRenewal = true;
                    }

                    currentMonths += row.DurationMonths;
                    currentExpiry = currentExpiry.AddMonths(row.DurationMonths);
                    continue;
                }

                hasComeback = true;
                if (currentMonths > longest)
                {
                    longest = currentMonths;
                }

                currentMonths = row.DurationMonths;
                currentExpiry = row.RedeemedAtUtc.AddMonths(row.DurationMonths);
            }

            if (currentMonths > longest)
            {
                longest = currentMonths;
            }

            return (longest, hasComeback, hasEarlyRenewal, onTimeRenewals, rows[0].RedeemedAtUtc);
        }

        private static bool IsMovieType(string itemType)
            => itemType.Equals("Movie", StringComparison.OrdinalIgnoreCase)
                || itemType.EndsWith(".Movie", StringComparison.OrdinalIgnoreCase);

        private static bool IsEpisodeType(string itemType)
            => itemType.Equals("Episode", StringComparison.OrdinalIgnoreCase)
                || itemType.EndsWith(".Episode", StringComparison.OrdinalIgnoreCase);

        private static DateTime ToInsightsLocalTime(DateTime utcDateTime)
        {
            var normalizedUtc = utcDateTime.Kind == DateTimeKind.Utc
                ? utcDateTime
                : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, _insightsTimeZone);
        }

        private static TimeZoneInfo ResolveInsightsTimeZone()
        {
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

        private static IEnumerable<string> SplitValues(string? values)
        {
            if (string.IsNullOrWhiteSpace(values))
            {
                yield break;
            }

            foreach (var part in values.Split(['|', ',', ';', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    yield return part;
                }
            }
        }

        private static string NormalizeGenreKey(string rawGenre)
        {
            var genre = rawGenre.Trim().ToLowerInvariant();
            if (genre.Contains("science fiction", StringComparison.Ordinal)
                || genre.Contains("sci-fi", StringComparison.Ordinal)
                || genre.Contains("sci fi", StringComparison.Ordinal)
                || genre.Contains("scifi", StringComparison.Ordinal))
            {
                return "scifi";
            }

            if (genre.Contains("biography", StringComparison.Ordinal) || genre.Contains("biopic", StringComparison.Ordinal))
            {
                return "biography";
            }

            if (genre.Contains("documentary", StringComparison.Ordinal))
            {
                return "documentary";
            }

            if (genre.Contains("animation", StringComparison.Ordinal))
            {
                return "animation";
            }

            if (genre.Contains("thriller", StringComparison.Ordinal))
            {
                return "thriller";
            }

            if (genre.Contains("mystery", StringComparison.Ordinal))
            {
                return "mystery";
            }

            if (genre.Contains("fantasy", StringComparison.Ordinal))
            {
                return "fantasy";
            }

            if (genre.Contains("romance", StringComparison.Ordinal))
            {
                return "romance";
            }

            if (genre.Contains("horror", StringComparison.Ordinal))
            {
                return "horror";
            }

            if (genre.Contains("history", StringComparison.Ordinal))
            {
                return "history";
            }

            if (genre.Contains("family", StringComparison.Ordinal))
            {
                return "family";
            }

            if (genre.Contains("action", StringComparison.Ordinal))
            {
                return "action";
            }

            if (genre.Contains("drama", StringComparison.Ordinal))
            {
                return "drama";
            }

            if (genre.Contains("comedy", StringComparison.Ordinal))
            {
                return "comedy";
            }

            if (genre.Contains("crime", StringComparison.Ordinal))
            {
                return "crime";
            }

            return genre;
        }

        private static string NormalizeCountryKey(string rawCountry)
            => rawCountry.Trim().ToLowerInvariant();

        private static bool IsInternationalTitle(IReadOnlyCollection<string> countries, string name, string originalTitle)
        {
            if (countries.Count > 0)
            {
                return countries.Any(country => !_homeCountryHints.Any(home => country.Contains(home, StringComparison.OrdinalIgnoreCase)));
            }

            return !string.IsNullOrWhiteSpace(originalTitle)
                && !string.Equals(name, originalTitle, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasConsecutive(IReadOnlyList<DateOnly> sortedDates, int minStreak)
            => LongestDaily(sortedDates) >= minStreak;

        private static bool HasInactivityGap(IReadOnlyList<DateOnly> sortedDates, int minGapDays)
        {
            if (sortedDates.Count < 2)
            {
                return false;
            }

            for (var index = 1; index < sortedDates.Count; index++)
            {
                if (sortedDates[index].DayNumber - sortedDates[index - 1].DayNumber >= minGapDays)
                {
                    return true;
                }
            }

            return false;
        }

        private static int LongestDaily(IReadOnlyList<DateOnly> sortedDates)
        {
            if (sortedDates.Count == 0)
            {
                return 0;
            }

            var longest = 1;
            var current = 1;
            for (var index = 1; index < sortedDates.Count; index++)
            {
                var diff = sortedDates[index].DayNumber - sortedDates[index - 1].DayNumber;
                if (diff == 1)
                {
                    current++;
                }
                else if (diff > 1)
                {
                    current = 1;
                }

                if (current > longest)
                {
                    longest = current;
                }
            }

            return longest;
        }

        private static int LongestWeekly(IReadOnlyList<DateOnly> sortedDates)
        {
            if (sortedDates.Count == 0)
            {
                return 0;
            }

            var weekStarts = sortedDates
                .Select(date =>
                {
                    var dt = date.ToDateTime(TimeOnly.MinValue);
                    var isoYear = ISOWeek.GetYear(dt);
                    var isoWeek = ISOWeek.GetWeekOfYear(dt);
                    return DateOnly.FromDateTime(ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Monday));
                })
                .Distinct()
                .OrderBy(date => date)
                .ToList();

            if (weekStarts.Count == 0)
            {
                return 0;
            }

            var longest = 1;
            var current = 1;
            for (var index = 1; index < weekStarts.Count; index++)
            {
                var diff = weekStarts[index].DayNumber - weekStarts[index - 1].DayNumber;
                if (diff == 7)
                {
                    current++;
                }
                else if (diff > 7)
                {
                    current = 1;
                }

                if (current > longest)
                {
                    longest = current;
                }
            }

            return longest;
        }

        private static int LongestMonthly(IReadOnlyList<DateOnly> sortedDates)
        {
            if (sortedDates.Count == 0)
            {
                return 0;
            }

            var months = sortedDates
                .Select(date => (date.Year * 12) + date.Month)
                .Distinct()
                .OrderBy(key => key)
                .ToList();

            if (months.Count == 0)
            {
                return 0;
            }

            var longest = 1;
            var current = 1;
            for (var index = 1; index < months.Count; index++)
            {
                var diff = months[index] - months[index - 1];
                if (diff == 1)
                {
                    current++;
                }
                else if (diff > 1)
                {
                    current = 1;
                }

                if (current > longest)
                {
                    longest = current;
                }
            }

            return longest;
        }

        private static DateTime? MinDate(params DateTime?[] values)
            => values
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .OrderBy(value => value)
                .Cast<DateTime?>()
                .FirstOrDefault();

        private static void Inc<TKey>(IDictionary<TKey, int> dict, TKey key)
            where TKey : notnull
        {
            if (!dict.TryAdd(key, 1))
            {
                dict[key]++;
            }
        }

        private static int GetCurrentSeasonYear(DateTime utcDateTime)
            => ToInsightsLocalTime(utcDateTime).Year;

        private static int? GetSeasonYear(AchievementDefinition definition, DateTime unlockTimestampUtc)
        {
            if (!definition.IsSeasonal)
            {
                return null;
            }

            return GetCurrentSeasonYear(unlockTimestampUtc);
        }

        private static string NormalizeAchievementId(string achievementId)
            => achievementId?.Trim().ToLowerInvariant() ?? string.Empty;

        private static AchievementDefinitionInfo ToDefinitionInfo(AchievementDefinition row)
            => new()
            {
                Id = row.Id,
                Title = row.Title,
                Description = row.Description,
                ImageEmoji = row.ImageEmoji,
                Rarity = row.Rarity,
                Xp = row.Xp,
                Coins = row.Coins,
                IsSeasonal = row.IsSeasonal,
                SeasonType = row.SeasonType
            };

        private static UserAchievementInfo ToUserAchievementInfo(AchievementDefinition row, DateTime unlockedAtUtc, int? seasonYear)
            => new()
            {
                Id = row.Id,
                Title = row.Title,
                Description = row.Description,
                ImageEmoji = row.ImageEmoji,
                Rarity = row.Rarity,
                Xp = row.Xp,
                Coins = row.Coins,
                UnlockedAt = unlockedAtUtc,
                IsSeasonal = row.IsSeasonal,
                SeasonType = row.SeasonType,
                SeasonYear = seasonYear
            };

        private sealed class Metrics
        {
            public int EarnedAchievementCoins { get; set; }

            public bool HasAnyWatch { get; set; }

            public int CompletedMovies { get; set; }

            public int CompletedEpisodes { get; set; }

            public int DistinctPlayedTitles { get; set; }

            public double TotalWatchHours { get; set; }

            public bool HasWeekendWatch { get; set; }

            public bool HasNightWatch { get; set; }

            public bool HasEarlyBirdWatch { get; set; }

            public int MaxTitlesInSingleDay { get; set; }

            public int MaxMoviesInSingleDay { get; set; }

            public int MaxEpisodesInSingleDay { get; set; }

            public bool HasCliffhangerSurvivor { get; set; }

            public int BingeSessionCount { get; set; }

            public int MaxBingeEpisodeCount { get; set; }

            public int MidnightEpisodeCount { get; set; }

            public int WeekendEpisodeCount { get; set; }

            public bool HasConsecutiveEpisodeDays { get; set; }

            public bool HasSeasonFinisher { get; set; }

            public bool HasOneSittingSeasonFinish { get; set; }

            public bool HasSeriesRewatch { get; set; }

            public bool HasClassicMovieWatch { get; set; }

            public bool HasNewReleaseWatch { get; set; }

            public bool HasLongHaulMovieWatch { get; set; }

            public bool HasShortStoryMovieWatch { get; set; }

            public Dictionary<string, int> GenreCounts { get; } = new(StringComparer.OrdinalIgnoreCase);

            public int UniqueGenreCount { get; set; }

            public int MaxGenreCount { get; set; }

            public int GenresWithAtLeastTwoTitles { get; set; }

            public int GenresWithAtLeastFiveTitles { get; set; }

            public int MajorGenresCovered { get; set; }

            public int UniqueCountryCount { get; set; }

            public int InternationalMovieCount { get; set; }

            public bool HasAwardWinnerWatch { get; set; }

            public int HighlyRatedTitles { get; set; }

            public int TotalRequests { get; set; }

            public int ApprovedOrCompletedRequests { get; set; }

            public int RequestActiveSpanDays { get; set; }

            public int RequestGenreDiversity { get; set; }

            public int PopularRequestCount { get; set; }

            public int WidelyWatchedRequestCount { get; set; }

            public int RequestSuccessRatioPercent { get; set; }

            public int LongestContinuousSubscriptionMonths { get; set; }

            public bool HasSubscriptionComeback { get; set; }

            public bool HasEarlyRenewal { get; set; }

            public int OnTimeRenewalCount { get; set; }

            public bool IsFoundingMember { get; set; }

            public int LongestDailyStreak { get; set; }

            public int LongestWeeklyStreak { get; set; }

            public int LongestMonthlyStreak { get; set; }

            public bool HasComebackAfterInactivity { get; set; }

            public int PrimeTimeSessionCount { get; set; }

            public bool IsPlatformVeteran { get; set; }
        }
    }
}
