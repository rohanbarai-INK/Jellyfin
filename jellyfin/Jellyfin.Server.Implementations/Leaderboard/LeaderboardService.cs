using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Leaderboard;
using MediaBrowser.Controller.Library;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Server.Implementations.Leaderboard
{
    /// <inheritdoc />
    public class LeaderboardService : ILeaderboardService
    {
        private const int DefaultTopLimit = 10;
        private const int MaxTopLimit = 100;
        private const int CacheTtlSeconds = 60;

        private static readonly string[] _validMetricTypes =
            ["xp", "watchtime", "movies", "series", "genres", "streak", "achievements", "requests"];

        private static readonly (int MinLevel, int MaxLevel, string Title, string Emoji)[] _rankTiers =
        [
            (1, 10, "Viewer", "\uD83D\uDC41\uFE0F"),
            (11, 20, "Explorer", "\uD83E\uDDED"),
            (21, 30, "Enthusiast", "\uD83C\uDFAC"),
            (31, 40, "Streamer", "\uD83D\uDCE1"),
            (41, 50, "Curator", "\uD83D\uDDC2\uFE0F"),
            (51, 60, "Collector", "\uD83D\uDCC0"),
            (61, 70, "Connoisseur", "\uD83C\uDF77"),
            (71, 80, "Elite", "\uD83D\uDEE1\uFE0F"),
            (81, 90, "Master", "\uD83C\uDFC6"),
            (91, 100, "Legend", "\uD83D\uDC51")
        ];

        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
        private readonly IUserManager _userManager;
        private readonly ConcurrentDictionary<string, (DateTime ExpiresUtc, IReadOnlyList<SeasonLeaderboardRow> Rows)> _seasonCache = new(StringComparer.Ordinal);
        private volatile bool _tableVerified;

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaderboardService"/> class.
        /// </summary>
        /// <param name="dbProvider">Database provider.</param>
        /// <param name="userManager">User manager.</param>
        public LeaderboardService(IDbContextFactory<JellyfinDbContext> dbProvider, IUserManager userManager)
        {
            _dbProvider = dbProvider;
            _userManager = userManager;
        }

        /// <inheritdoc />
        public async Task<LeaderboardPersonalInfo> GetPersonalStats(Guid userId, int seasonYear, string metricType = "xp")
        {
            var metric = NormalizeMetricType(metricType);
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                _ = await GetOrCreateSeasonStats(dbContext, userId, seasonYear).ConfigureAwait(false);
            }

            var rows = await GetSeasonRowsAsync(seasonYear, metric).ConfigureAwait(false);
            var currentIndex = FindRowIndex(rows, userId);
            if (currentIndex < 0)
            {
                InvalidateCache(seasonYear);
                rows = await GetSeasonRowsAsync(seasonYear, metric).ConfigureAwait(false);
                currentIndex = FindRowIndex(rows, userId);
            }

            if (currentIndex < 0)
            {
                throw new InvalidOperationException("Unable to resolve leaderboard entry for the current user.");
            }

            var currentRow = rows[currentIndex];
            var rank = currentIndex + 1;
            var totalUsers = rows.Count;
            var percentile = totalUsers <= 1 ? 100.0 : Math.Round(((totalUsers - rank) / (double)(totalUsers - 1)) * 100.0, 1);
            var (rankTitle, rankEmoji) = GetRankTier(currentRow.Level);
            var currentUserEntry = BuildEntry(currentRow, metric, seasonYear, rank, true);
            var nextTarget = currentIndex > 0
                ? BuildCompetitionInfo(rows[currentIndex - 1], metric, seasonYear, rank - 1, GetMetricValueFromRow(rows[currentIndex - 1], metric) - currentUserEntry.MetricValue)
                : null;
            var behindUser = currentIndex + 1 < rows.Count
                ? BuildCompetitionInfo(rows[currentIndex + 1], metric, seasonYear, rank + 1, currentUserEntry.MetricValue - GetMetricValueFromRow(rows[currentIndex + 1], metric))
                : null;
            var topMetricValue = rows.Count > 0 ? GetMetricValueFromRow(rows[0], metric) : currentUserEntry.MetricValue;

            return new LeaderboardPersonalInfo
            {
                SeasonYear = seasonYear,
                TotalXp = currentRow.TotalXp,
                AchievementXp = currentRow.AchievementXp,
                AchievementCount = currentRow.AchievementCount,
                Level = currentRow.Level,
                Rank = rank,
                Percentile = percentile,
                TotalUsers = totalUsers,
                RankTitle = rankTitle,
                RankEmoji = rankEmoji,
                TotalWatchMinutes = currentRow.TotalWatchMinutes,
                MoviesCompleted = currentRow.MoviesCompleted,
                SeriesCompleted = currentRow.SeriesCompleted,
                UniqueGenresWatched = currentRow.UniqueGenresWatched,
                CurrentStreakDays = currentRow.CurrentStreakDays,
                BestStreakDays = currentRow.BestStreakDays,
                AchievementsUnlocked = currentRow.AchievementsUnlocked,
                ApprovedRequests = currentRow.ApprovedRequests,
                MetricValue = currentUserEntry.MetricValue,
                MetricType = metric,
                MetricLabel = currentUserEntry.MetricLabel,
                GapToNext = nextTarget?.GapValue ?? 0,
                GapToTop = Math.Max(0, topMetricValue - currentUserEntry.MetricValue),
                CurrentUserEntry = currentUserEntry,
                NextTarget = nextTarget,
                BehindUser = behindUser
            };
        }

        /// <inheritdoc />
        public async Task<LeaderboardTopResult> GetTopLeaderboard(Guid requestingUserId, int seasonYear, int offset, int limit, string metricType = "xp")
        {
            var metric = NormalizeMetricType(metricType);
            var normalizedOffset = Math.Max(0, offset);
            var normalizedLimit = limit <= 0 ? DefaultTopLimit : Math.Min(limit, MaxTopLimit);

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                _ = await GetOrCreateSeasonStats(dbContext, requestingUserId, seasonYear).ConfigureAwait(false);
            }

            var rows = await GetSeasonRowsAsync(seasonYear, metric).ConfigureAwait(false);
            var currentUserIndex = FindRowIndex(rows, requestingUserId);
            if (currentUserIndex < 0)
            {
                InvalidateCache(seasonYear);
                rows = await GetSeasonRowsAsync(seasonYear, metric).ConfigureAwait(false);
                currentUserIndex = FindRowIndex(rows, requestingUserId);
            }

            var pagedRows = rows.Skip(normalizedOffset).Take(normalizedLimit).ToList();
            var entries = new List<LeaderboardEntryInfo>(pagedRows.Count);
            for (var i = 0; i < pagedRows.Count; i++)
            {
                var rank = normalizedOffset + i + 1;
                entries.Add(BuildEntry(pagedRows[i], metric, seasonYear, rank, pagedRows[i].UserId.Equals(requestingUserId)));
            }

            LeaderboardEntryInfo? currentUserPosition = null;
            if (currentUserIndex >= 0 && (currentUserIndex < normalizedOffset || currentUserIndex >= normalizedOffset + pagedRows.Count))
            {
                currentUserPosition = BuildEntry(rows[currentUserIndex], metric, seasonYear, currentUserIndex + 1, true);
            }

            return new LeaderboardTopResult
            {
                SeasonYear = seasonYear,
                MetricType = metric,
                Entries = entries,
                CurrentUserPosition = currentUserPosition,
                TotalUsers = rows.Count,
                Offset = normalizedOffset,
                Limit = normalizedLimit,
                HasMore = normalizedOffset + pagedRows.Count < rows.Count
            };
        }

        /// <inheritdoc />
        public async Task RecordAchievementXp(Guid userId, int seasonYear, int xp, int coins)
        {
            if (xp <= 0)
            {
                return;
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableExistsAsync(dbContext).ConfigureAwait(false);

                var stats = await dbContext.UserSeasonStats
                    .FirstOrDefaultAsync(s => s.UserId.Equals(userId) && s.SeasonYear == seasonYear)
                    .ConfigureAwait(false);

                if (stats is null)
                {
                    stats = await ComputeAndCreateStats(dbContext, userId, seasonYear).ConfigureAwait(false);
                }
                else
                {
                    stats.AchievementXp += xp;
                    stats.TotalXp += xp;
                    stats.AchievementCount++;
                    stats.AchievementsUnlocked++;
                    stats.Level = ComputeLevel(stats.TotalXp);
                    stats.LastUpdatedUtc = DateTime.UtcNow;
                }

                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }

            InvalidateCache(seasonYear);
        }

        /// <inheritdoc />
        public async Task RecordPlaybackStats(Guid userId, int seasonYear, long validatedMinutes, bool movieCompleted, bool episodeCompleted, string[] genres)
        {
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableExistsAsync(dbContext).ConfigureAwait(false);

                var stats = await dbContext.UserSeasonStats
                    .FirstOrDefaultAsync(s => s.UserId.Equals(userId) && s.SeasonYear == seasonYear)
                    .ConfigureAwait(false);

                if (stats is null)
                {
                    stats = await ComputeAndCreateStats(dbContext, userId, seasonYear).ConfigureAwait(false);
                }

                stats.TotalWatchMinutes += validatedMinutes;

                if (movieCompleted)
                {
                    stats.MoviesCompleted++;
                }

                if (episodeCompleted)
                {
                    stats.SeriesCompleted++;
                }

                if (genres.Length > 0)
                {
                    // Recompute unique genres from UserGenrePeriodStats for the year
                    var yearKey = seasonYear.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var uniqueGenreCount = await dbContext.UserGenrePeriodStats
                        .AsNoTracking()
                        .Where(g => g.UserId.Equals(userId)
                            && g.PeriodType == Jellyfin.Database.Implementations.Enums.PeriodType.Year
                            && g.PeriodKey == yearKey)
                        .Select(g => g.GenreId)
                        .Distinct()
                        .CountAsync()
                        .ConfigureAwait(false);

                    stats.UniqueGenresWatched = uniqueGenreCount;
                }

                // Streak logic: if user was active yesterday, increment; if today already counted, skip; otherwise reset to 1
                var todayUtc = DateTime.UtcNow.Date;
                var lastActiveDate = stats.LastActiveUtc.Date;
                if (lastActiveDate == todayUtc)
                {
                    // Already active today, no streak change
                }
                else if (lastActiveDate == todayUtc.AddDays(-1))
                {
                    stats.CurrentStreakDays++;
                }
                else
                {
                    stats.CurrentStreakDays = 1;
                }

                if (stats.CurrentStreakDays > stats.BestStreakDays)
                {
                    stats.BestStreakDays = stats.CurrentStreakDays;
                }

                stats.LastActiveUtc = DateTime.UtcNow;
                stats.LastUpdatedUtc = DateTime.UtcNow;

                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }

            InvalidateCache(seasonYear);
        }

        /// <inheritdoc />
        public async Task RecordApprovedRequest(Guid userId, int seasonYear)
        {
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableExistsAsync(dbContext).ConfigureAwait(false);

                var stats = await dbContext.UserSeasonStats
                    .FirstOrDefaultAsync(s => s.UserId.Equals(userId) && s.SeasonYear == seasonYear)
                    .ConfigureAwait(false);

                if (stats is null)
                {
                    stats = await ComputeAndCreateStats(dbContext, userId, seasonYear).ConfigureAwait(false);
                }

                stats.ApprovedRequests++;
                stats.LastUpdatedUtc = DateTime.UtcNow;

                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }

            InvalidateCache(seasonYear);
        }

        private async Task EnsureTableExistsAsync(JellyfinDbContext dbContext)
        {
            if (_tableVerified)
            {
                return;
            }

            const string Sql = @"
                CREATE TABLE IF NOT EXISTS UserSeasonStats (
                    Id TEXT NOT NULL PRIMARY KEY,
                    UserId TEXT NOT NULL,
                    SeasonYear INTEGER NOT NULL,
                    TotalXp INTEGER NOT NULL DEFAULT 0,
                    AchievementXp INTEGER NOT NULL DEFAULT 0,
                    AchievementCount INTEGER NOT NULL DEFAULT 0,
                    Level INTEGER NOT NULL DEFAULT 0,
                    TotalWatchMinutes INTEGER NOT NULL DEFAULT 0,
                    MoviesCompleted INTEGER NOT NULL DEFAULT 0,
                    SeriesCompleted INTEGER NOT NULL DEFAULT 0,
                    UniqueGenresWatched INTEGER NOT NULL DEFAULT 0,
                    CurrentStreakDays INTEGER NOT NULL DEFAULT 0,
                    BestStreakDays INTEGER NOT NULL DEFAULT 0,
                    AchievementsUnlocked INTEGER NOT NULL DEFAULT 0,
                    ApprovedRequests INTEGER NOT NULL DEFAULT 0,
                    LastActiveUtc TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                    LastUpdatedUtc TEXT NOT NULL,
                    CONSTRAINT FK_UserSeasonStats_Users_UserId FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_UserSeasonStats_UserId_SeasonYear ON UserSeasonStats (UserId, SeasonYear);
                CREATE INDEX IF NOT EXISTS IX_UserSeasonStats_SeasonYear_TotalXp ON UserSeasonStats (SeasonYear, TotalXp);
                CREATE INDEX IF NOT EXISTS IX_UserSeasonStats_SeasonYear_WatchMinutes ON UserSeasonStats (SeasonYear, TotalWatchMinutes);
                CREATE INDEX IF NOT EXISTS IX_UserSeasonStats_SeasonYear_Movies ON UserSeasonStats (SeasonYear, MoviesCompleted);
                CREATE INDEX IF NOT EXISTS IX_UserSeasonStats_SeasonYear_Series ON UserSeasonStats (SeasonYear, SeriesCompleted);
                CREATE INDEX IF NOT EXISTS IX_UserSeasonStats_SeasonYear_Genres ON UserSeasonStats (SeasonYear, UniqueGenresWatched);
                CREATE INDEX IF NOT EXISTS IX_UserSeasonStats_SeasonYear_Streak ON UserSeasonStats (SeasonYear, CurrentStreakDays);
                CREATE INDEX IF NOT EXISTS IX_UserSeasonStats_SeasonYear_Achievements ON UserSeasonStats (SeasonYear, AchievementsUnlocked);
                CREATE INDEX IF NOT EXISTS IX_UserSeasonStats_SeasonYear_Requests ON UserSeasonStats (SeasonYear, ApprovedRequests);
            ";

            // Also add missing columns for existing tables (ALTER TABLE IF NOT EXISTS workaround for SQLite)
            const string AlterSql = @"
                ALTER TABLE UserSeasonStats ADD COLUMN TotalWatchMinutes INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE UserSeasonStats ADD COLUMN MoviesCompleted INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE UserSeasonStats ADD COLUMN SeriesCompleted INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE UserSeasonStats ADD COLUMN UniqueGenresWatched INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE UserSeasonStats ADD COLUMN CurrentStreakDays INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE UserSeasonStats ADD COLUMN BestStreakDays INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE UserSeasonStats ADD COLUMN AchievementsUnlocked INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE UserSeasonStats ADD COLUMN ApprovedRequests INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE UserSeasonStats ADD COLUMN LastActiveUtc TEXT NOT NULL DEFAULT '0001-01-01 00:00:00';
            ";

            await dbContext.Database.ExecuteSqlRawAsync(Sql).ConfigureAwait(false);

            // Try adding new columns to existing table - ignore errors for columns that already exist
            foreach (var alterLine in AlterSql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.IsNullOrWhiteSpace(alterLine))
                {
                    continue;
                }

                try
                {
#pragma warning disable EF1003
                    await dbContext.Database.ExecuteSqlRawAsync(alterLine + ";").ConfigureAwait(false);
#pragma warning restore EF1003
                }
                catch (Microsoft.Data.Sqlite.SqliteException)
                {
                    // Column already exists - expected
                }
            }

            _tableVerified = true;
        }

        private async Task<UserSeasonStats> GetOrCreateSeasonStats(JellyfinDbContext dbContext, Guid userId, int seasonYear)
        {
            await EnsureTableExistsAsync(dbContext).ConfigureAwait(false);

            var stats = await dbContext.UserSeasonStats
                .FirstOrDefaultAsync(s => s.UserId.Equals(userId) && s.SeasonYear == seasonYear)
                .ConfigureAwait(false);

            if (stats is not null)
            {
                return stats;
            }

            return await ComputeAndCreateStats(dbContext, userId, seasonYear).ConfigureAwait(false);
        }

        private static async Task<UserSeasonStats> ComputeAndCreateStats(JellyfinDbContext dbContext, Guid userId, int seasonYear)
        {
            var seasonStart = new DateTime(seasonYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var seasonEnd = new DateTime(seasonYear + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var aggregation = await (
                from unlock in dbContext.UserAchievements.AsNoTracking()
                join definition in dbContext.AchievementDefinitions.AsNoTracking()
                    on unlock.AchievementId equals definition.Id
                where unlock.UserId.Equals(userId)
                    && unlock.UnlockedAtUtc >= seasonStart
                    && unlock.UnlockedAtUtc < seasonEnd
                group definition by 1 into grouped
                select new
                {
                    TotalXp = grouped.Sum(d => (long)d.Xp),
                    Count = grouped.Count()
                })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            var achievementXp = aggregation?.TotalXp ?? 0;
            var achievementCount = aggregation?.Count ?? 0;
            var totalXp = achievementXp;

            var stats = new UserSeasonStats
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SeasonYear = seasonYear,
                TotalXp = totalXp,
                AchievementXp = achievementXp,
                AchievementCount = achievementCount,
                AchievementsUnlocked = achievementCount,
                Level = ComputeLevel(totalXp),
                BestStreakDays = 0,
                LastActiveUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            };

            dbContext.UserSeasonStats.Add(stats);

            try
            {
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                // Possible race condition - re-read
                dbContext.Entry(stats).State = EntityState.Detached;
                var existing = await dbContext.UserSeasonStats
                    .FirstOrDefaultAsync(s => s.UserId.Equals(userId) && s.SeasonYear == seasonYear)
                    .ConfigureAwait(false);

                if (existing is not null)
                {
                    return existing;
                }

                throw;
            }

            return stats;
        }

        private static string NormalizeMetricType(string metricType)
        {
            if (string.IsNullOrWhiteSpace(metricType))
            {
                return "xp";
            }

            var lower = metricType.Trim().ToLowerInvariant();
            return Array.Exists(_validMetricTypes, m => m == lower) ? lower : "xp";
        }

        private async Task<IReadOnlyList<SeasonLeaderboardRow>> GetSeasonRowsAsync(int seasonYear, string metric)
        {
            var cacheKey = $"leaderboard_rows_{metric}_{seasonYear}";
            if (_seasonCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresUtc > DateTime.UtcNow)
            {
                return cached.Rows;
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableExistsAsync(dbContext).ConfigureAwait(false);

                var query = dbContext.UserSeasonStats
                    .AsNoTracking()
                    .Where(s => s.SeasonYear == seasonYear);

                var rows = await ApplyMetricOrdering(query, metric)
                    .Select(s => new SeasonLeaderboardRow(
                        s.UserId,
                        s.TotalXp,
                        s.AchievementXp,
                        s.AchievementCount,
                        s.Level,
                        s.TotalWatchMinutes,
                        s.MoviesCompleted,
                        s.SeriesCompleted,
                        s.UniqueGenresWatched,
                        s.CurrentStreakDays,
                        s.BestStreakDays,
                        s.AchievementsUnlocked,
                        s.ApprovedRequests,
                        s.LastActiveUtc,
                        s.LastUpdatedUtc))
                    .ToListAsync()
                    .ConfigureAwait(false);

                _seasonCache[cacheKey] = (DateTime.UtcNow.AddSeconds(CacheTtlSeconds), rows);
                return rows;
            }
        }

        private LeaderboardEntryInfo BuildEntry(SeasonLeaderboardRow row, string metric, int seasonYear, int rank, bool isCurrentUser)
        {
            var (userName, primaryImageTag) = ResolveUserIdentity(row.UserId);
            var metricValue = GetMetricValueFromRow(row, metric);
            var secondaryMetricValue = GetSecondaryMetricValue(row, metric, seasonYear);
            var (rankTitle, rankEmoji) = GetRankTier(row.Level);

            return new LeaderboardEntryInfo
            {
                UserId = row.UserId,
                UserName = userName,
                PrimaryImageTag = primaryImageTag,
                Rank = rank,
                TotalXp = row.TotalXp,
                Level = row.Level,
                AchievementCount = row.AchievementCount,
                RankTitle = rankTitle,
                RankEmoji = rankEmoji,
                BadgeKind = GetBadgeKind(rank),
                IsCurrentUser = isCurrentUser,
                MetricValue = metricValue,
                MetricLabel = FormatMetricLabel(metricValue, metric),
                SecondaryMetricValue = secondaryMetricValue,
                SecondaryMetricLabel = FormatSecondaryMetricLabel(secondaryMetricValue, metric)
            };
        }

        private LeaderboardCompetitionInfo BuildCompetitionInfo(SeasonLeaderboardRow row, string metric, int seasonYear, int rank, long gapValue)
        {
            var normalizedGap = Math.Max(0, gapValue);

            return new LeaderboardCompetitionInfo
            {
                Entry = BuildEntry(row, metric, seasonYear, rank, false),
                GapValue = normalizedGap,
                GapLabel = FormatMetricLabel(normalizedGap, metric)
            };
        }

        private (string UserName, string PrimaryImageTag) ResolveUserIdentity(Guid userId)
        {
            var user = _userManager.GetUserById(userId);
            if (user is null)
            {
                return ("Unknown User", string.Empty);
            }

            var userDto = _userManager.GetUserDto(user);
            var userName = string.IsNullOrWhiteSpace(userDto.Name) ? user.Username : userDto.Name;
            return (userName, userDto.PrimaryImageTag ?? string.Empty);
        }

        private static int FindRowIndex(IReadOnlyList<SeasonLeaderboardRow> rows, Guid userId)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].UserId.Equals(userId))
                {
                    return i;
                }
            }

            return -1;
        }

        private static long GetMetricValueFromRow(SeasonLeaderboardRow row, string metric) => metric switch
        {
            "watchtime" => row.TotalWatchMinutes,
            "movies" => row.MoviesCompleted,
            "series" => row.SeriesCompleted,
            "genres" => row.UniqueGenresWatched,
            "streak" => row.CurrentStreakDays,
            "achievements" => row.AchievementsUnlocked,
            "requests" => row.ApprovedRequests,
            _ => row.TotalXp
        };

        private static long GetSecondaryMetricValue(SeasonLeaderboardRow row, string metric, int seasonYear)
        {
            var elapsedDays = GetSeasonElapsedDays(seasonYear);

            return metric switch
            {
                "watchtime" => (long)Math.Round(row.TotalWatchMinutes * 7d / elapsedDays, MidpointRounding.AwayFromZero),
                "movies" => row.TotalWatchMinutes,
                "series" => row.TotalWatchMinutes,
                "genres" => (long)Math.Round(row.UniqueGenresWatched * 30d / elapsedDays, MidpointRounding.AwayFromZero),
                "streak" => row.BestStreakDays,
                "achievements" => row.AchievementXp,
                "requests" => row.TotalXp,
                _ => row.Level
            };
        }

        private static string FormatMetricLabel(long value, string metric) => metric switch
        {
            "watchtime" => FormatMinutesLabel(value),
            "movies" => $"{value} movies",
            "series" => $"{value} episodes",
            "genres" => $"{value} genres",
            "streak" => $"{value} days",
            "achievements" => $"{value} achievements",
            "requests" => $"{value} requests",
            _ => $"{value} XP"
        };

        private static string FormatSecondaryMetricLabel(long value, string metric) => metric switch
        {
            "watchtime" => $"{FormatMinutesLabel(value)}/wk",
            "movies" => $"{FormatMinutesLabel(value)} watched",
            "series" => $"{FormatMinutesLabel(value)} watched",
            "genres" => $"{value}/mo",
            "streak" => $"Best {value} days",
            "achievements" => $"{value} XP",
            "requests" => $"{value} XP",
            _ => $"Lv {value}"
        };

        private static string FormatMinutesLabel(long value)
        {
            if (value >= 60)
            {
                var hours = value / 60d;
                var rounded = value >= 600 ? Math.Round(hours, 0) : Math.Round(hours, 1);
                return rounded % 1 == 0
                    ? $"{rounded:0} hrs"
                    : $"{rounded:0.#} hrs";
            }

            return $"{value} min";
        }

        private static int GetSeasonElapsedDays(int seasonYear)
        {
            var seasonStart = new DateTime(seasonYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var seasonEnd = seasonStart.AddYears(1);
            var boundary = DateTime.UtcNow < seasonEnd ? DateTime.UtcNow : seasonEnd;
            return Math.Max(1, (int)Math.Ceiling((boundary - seasonStart).TotalDays));
        }

        private static string GetBadgeKind(int rank) => rank switch
        {
            1 => "gold",
            2 => "silver",
            3 => "bronze",
            <= 10 => "top10",
            _ => "none"
        };

        private static IQueryable<UserSeasonStats> ApplyMetricOrdering(IQueryable<UserSeasonStats> query, string metric) => metric switch
        {
            "watchtime" => query.OrderByDescending(s => s.TotalWatchMinutes).ThenBy(s => s.LastUpdatedUtc).ThenBy(s => s.UserId),
            "movies" => query.OrderByDescending(s => s.MoviesCompleted).ThenBy(s => s.LastUpdatedUtc).ThenBy(s => s.UserId),
            "series" => query.OrderByDescending(s => s.SeriesCompleted).ThenBy(s => s.LastUpdatedUtc).ThenBy(s => s.UserId),
            "genres" => query.OrderByDescending(s => s.UniqueGenresWatched).ThenBy(s => s.LastUpdatedUtc).ThenBy(s => s.UserId),
            "streak" => query.OrderByDescending(s => s.CurrentStreakDays).ThenBy(s => s.LastUpdatedUtc).ThenBy(s => s.UserId),
            "achievements" => query.OrderByDescending(s => s.AchievementsUnlocked).ThenBy(s => s.LastUpdatedUtc).ThenBy(s => s.UserId),
            "requests" => query.OrderByDescending(s => s.ApprovedRequests).ThenBy(s => s.LastUpdatedUtc).ThenBy(s => s.UserId),
            _ => query.OrderByDescending(s => s.TotalXp).ThenBy(s => s.LastUpdatedUtc).ThenBy(s => s.UserId)
        };

        private void InvalidateCache(int seasonYear)
        {
            foreach (var key in _seasonCache.Keys)
            {
                if (key.EndsWith($"_{seasonYear}", StringComparison.Ordinal))
                {
                    _seasonCache.TryRemove(key, out _);
                }
            }
        }

        private static int ComputeLevel(long totalXp)
        {
            const double xpCurveBase = 35.0;
            const double xpCurveExponent = 1.2;

            var level = 0;
            var spentXp = 0L;

            while (true)
            {
                var nextLevel = level + 1;
                var xpRequired = (long)Math.Max(1, Math.Round(xpCurveBase * Math.Pow(nextLevel, xpCurveExponent)));
                if (spentXp + xpRequired > totalXp)
                {
                    break;
                }

                spentXp += xpRequired;
                level = nextLevel;
            }

            return level;
        }

        private static (string Title, string Emoji) GetRankTier(int level)
        {
            var normalizedLevel = Math.Max(1, Math.Min(100, level));
            foreach (var (minLevel, maxLevel, title, emoji) in _rankTiers)
            {
                if (normalizedLevel >= minLevel && normalizedLevel <= maxLevel)
                {
                    return (title, emoji);
                }
            }

            return (_rankTiers[0].Title, _rankTiers[0].Emoji);
        }

        private sealed record SeasonLeaderboardRow(
            Guid UserId,
            long TotalXp,
            long AchievementXp,
            int AchievementCount,
            int Level,
            long TotalWatchMinutes,
            int MoviesCompleted,
            int SeriesCompleted,
            int UniqueGenresWatched,
            int CurrentStreakDays,
            int BestStreakDays,
            int AchievementsUnlocked,
            int ApprovedRequests,
            DateTime LastActiveUtc,
            DateTime LastUpdatedUtc);
    }
}
