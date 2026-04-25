using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Trending;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Server.Implementations.Tracking
{
    /// <summary>
    /// Computes the neutral global Trending Now candidate set.
    /// </summary>
    public class TrendingBaseService
    {
        private static readonly string[] _episodeTypeAliases = [nameof(MediaBrowser.Controller.Entities.TV.Episode), typeof(MediaBrowser.Controller.Entities.TV.Episode).FullName ?? nameof(MediaBrowser.Controller.Entities.TV.Episode)];
        private static readonly string[] _movieTypeAliases = [nameof(MediaBrowser.Controller.Entities.Movies.Movie), typeof(MediaBrowser.Controller.Entities.Movies.Movie).FullName ?? nameof(MediaBrowser.Controller.Entities.Movies.Movie)];
        private static readonly TimeZoneInfo _trendingTimeZone = ResolveTrendingTimeZone();

        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrendingBaseService"/> class.
        /// </summary>
        /// <param name="dbProvider">Database provider.</param>
        /// <param name="timeProvider">Time provider.</param>
        public TrendingBaseService(IDbContextFactory<JellyfinDbContext> dbProvider, TimeProvider timeProvider)
        {
            _dbProvider = dbProvider;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Gets the global trending candidates for the requested period.
        /// </summary>
        /// <param name="periodType">Requested period.</param>
        /// <param name="candidateLimit">Maximum candidate count.</param>
        /// <returns>Candidate result.</returns>
        public async Task<TrendingBaseCandidateSet> GetBaseCandidates(TrendingNowPeriodType periodType, int candidateLimit)
        {
            var normalizedCandidateLimit = Math.Clamp(candidateLimit, 1, 80);
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var period = ResolvePeriod(periodType, nowUtc);
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var aggregates = await (
                    from session in dbContext.UserWatchSessions.AsNoTracking()
                    join item in dbContext.BaseItems.AsNoTracking() on session.ItemId equals item.Id
                    let isEpisode = _episodeTypeAliases.Contains(item.Type)
                    let isMovie = _movieTypeAliases.Contains(item.Type)
                    let targetItemId = isEpisode && item.SeriesId.HasValue ? item.SeriesId.Value : item.Id
                    where session.IsValidSession
                        && session.ValidatedTicks > 0
                        && session.StartTimeUtc >= period.PeriodStartUtc
                        && session.StartTimeUtc < period.PeriodEndUtc
                        && item.MediaType == "Video"
                        && !item.IsFolder
                        && (isMovie || (isEpisode && item.SeriesId.HasValue))
                    group new { session, item } by targetItemId into grouped
                    select new AggregateRow
                    {
                        ItemId = grouped.Key,
                        TotalValidatedTicks = grouped.Sum(row => row.session.ValidatedTicks),
                        UniqueViewers = grouped.Select(row => row.session.UserId).Distinct().Count(),
                        Starts = grouped.Count(),
                        Completions = grouped.Count(row => row.item.RunTimeTicks.HasValue
                            && row.item.RunTimeTicks.Value > 0
                            && row.session.ValidatedTicks >= ((row.item.RunTimeTicks.Value * 9L) / 10L)),
                        MomentumTicks = grouped
                            .Where(row => row.session.StartTimeUtc >= period.MomentumStartUtc)
                            .Sum(row => (long?)row.session.ValidatedTicks) ?? 0L
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (aggregates.Count == 0)
                {
                    return new TrendingBaseCandidateSet(period, [], 0, true);
                }

                var itemIds = aggregates.Select(row => row.ItemId).Distinct().ToArray();
                var metadataRows = await dbContext.BaseItems
                    .AsNoTracking()
                    .Where(item => itemIds.Contains(item.Id))
                    .Select(item => new MetadataRow
                    {
                        ItemId = item.Id,
                        ItemType = item.Type ?? string.Empty,
                        Title = item.Name ?? string.Empty,
                        Overview = item.Overview ?? string.Empty,
                        Genres = item.Genres ?? string.Empty,
                        ProductionYear = item.ProductionYear,
                        RunTimeTicks = item.RunTimeTicks,
                        OfficialRating = item.OfficialRating ?? string.Empty
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

                var imageRows = await dbContext.BaseItemImageInfos
                    .AsNoTracking()
                    .Where(image => itemIds.Contains(image.ItemId))
                    .Select(image => new ImageRow
                    {
                        ItemId = image.ItemId,
                        ImageType = image.ImageType
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

                var metadataLookup = metadataRows.ToDictionary(row => row.ItemId);
                var imageLookup = imageRows
                    .GroupBy(row => row.ItemId)
                    .ToDictionary(
                        group => group.Key,
                        group => new ImageSummary(
                            group.Any(image => image.ImageType == ImageInfoImageType.Primary),
                            group.Any(image => image.ImageType == ImageInfoImageType.Backdrop)));

                var candidates = aggregates
                    .Where(row => metadataLookup.ContainsKey(row.ItemId))
                    .Select(row =>
                    {
                        var metadata = metadataLookup[row.ItemId];
                        var images = imageLookup.TryGetValue(row.ItemId, out var summary)
                            ? summary
                            : new ImageSummary(false, false);
                        var totalWatchHours = row.TotalValidatedTicks / (double)TimeSpan.TicksPerHour;
                        var momentumWatchHours = row.MomentumTicks / (double)TimeSpan.TicksPerHour;
                        var completionRate = row.Starts > 0 ? (double)row.Completions / row.Starts : 0D;
                        var freshnessRatio = row.TotalValidatedTicks > 0
                            ? Math.Clamp((double)row.MomentumTicks / row.TotalValidatedTicks, 0D, 1D)
                            : 0D;

                        return new TrendingBaseCandidate
                        {
                            ItemId = row.ItemId,
                            ItemType = metadata.ItemType,
                            Title = string.IsNullOrWhiteSpace(metadata.Title) ? "Unknown title" : metadata.Title.Trim(),
                            TotalWatchHours = totalWatchHours,
                            UniqueViewers = row.UniqueViewers,
                            Starts = row.Starts,
                            Completions = row.Completions,
                            MomentumWatchHours = momentumWatchHours,
                            BaseScore = ComputeBaseScore(totalWatchHours, row.UniqueViewers, row.Starts, row.Completions, momentumWatchHours, completionRate, freshnessRatio),
                            ContextText = BuildContextText(row.UniqueViewers, totalWatchHours, row.Starts),
                            Overview = metadata.Overview.Trim(),
                            Genres = ParseGenres(metadata.Genres),
                            ProductionYear = metadata.ProductionYear,
                            RunTimeTicks = metadata.RunTimeTicks,
                            OfficialRating = metadata.OfficialRating.Trim(),
                            HasPrimaryImage = images.HasPrimary,
                            HasBackdropImage = images.HasBackdrop
                        };
                    })
                    .OrderByDescending(row => row.BaseScore)
                    .ThenByDescending(row => row.UniqueViewers)
                    .ThenByDescending(row => row.MomentumWatchHours)
                    .ThenByDescending(row => row.TotalWatchHours)
                    .ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
                    .Take(normalizedCandidateLimit)
                    .ToList();

                var usedFallbackMode = candidates.Count < Math.Min(8, normalizedCandidateLimit);
                return new TrendingBaseCandidateSet(period, candidates, aggregates.Count, usedFallbackMode);
            }
        }

        /// <summary>
        /// Loads metadata-only candidates for forced editorial inclusions.
        /// </summary>
        /// <param name="itemIds">Item ids to load.</param>
        /// <returns>Metadata-only candidates with neutral scores.</returns>
        public async Task<IReadOnlyList<TrendingBaseCandidate>> GetCandidatesByIds(IEnumerable<Guid> itemIds)
        {
            var ids = itemIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray();
            if (ids.Length == 0)
            {
                return Array.Empty<TrendingBaseCandidate>();
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var metadataRows = await dbContext.BaseItems
                    .AsNoTracking()
                    .Where(item => ids.Contains(item.Id) && item.MediaType == "Video" && !item.IsFolder)
                    .Select(item => new MetadataRow
                    {
                        ItemId = item.Id,
                        ItemType = item.Type ?? string.Empty,
                        Title = item.Name ?? string.Empty,
                        Overview = item.Overview ?? string.Empty,
                        Genres = item.Genres ?? string.Empty,
                        ProductionYear = item.ProductionYear,
                        RunTimeTicks = item.RunTimeTicks,
                        OfficialRating = item.OfficialRating ?? string.Empty
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

                var imageRows = await dbContext.BaseItemImageInfos
                    .AsNoTracking()
                    .Where(image => ids.Contains(image.ItemId))
                    .Select(image => new ImageRow
                    {
                        ItemId = image.ItemId,
                        ImageType = image.ImageType
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

                var imageLookup = imageRows
                    .GroupBy(row => row.ItemId)
                    .ToDictionary(
                        group => group.Key,
                        group => new ImageSummary(
                            group.Any(image => image.ImageType == ImageInfoImageType.Primary),
                            group.Any(image => image.ImageType == ImageInfoImageType.Backdrop)));

                return metadataRows
                    .Select(metadata =>
                    {
                        var images = imageLookup.TryGetValue(metadata.ItemId, out var summary)
                            ? summary
                            : new ImageSummary(false, false);
                        return new TrendingBaseCandidate
                        {
                            ItemId = metadata.ItemId,
                            ItemType = metadata.ItemType,
                            Title = string.IsNullOrWhiteSpace(metadata.Title) ? "Unknown title" : metadata.Title.Trim(),
                            BaseScore = 0D,
                            TotalWatchHours = 0D,
                            UniqueViewers = 0,
                            Starts = 0,
                            Completions = 0,
                            MomentumWatchHours = 0D,
                            ContextText = "Featured by KnightFlix",
                            Overview = metadata.Overview.Trim(),
                            Genres = ParseGenres(metadata.Genres),
                            ProductionYear = metadata.ProductionYear,
                            RunTimeTicks = metadata.RunTimeTicks,
                            OfficialRating = metadata.OfficialRating.Trim(),
                            HasPrimaryImage = images.HasPrimary,
                            HasBackdropImage = images.HasBackdrop
                        };
                    })
                    .ToList();
            }
        }

        private static double ComputeBaseScore(double watchHours, int uniqueViewers, int starts, int completions, double momentumHours, double completionRate, double freshnessRatio)
            => Math.Round(
                (watchHours * 5.5D)
                + (uniqueViewers * 16D)
                + (starts * 4D)
                + (completions * 12D)
                + (momentumHours * 7D)
                + (completionRate * 10D)
                + (freshnessRatio * 8D),
                2,
                MidpointRounding.AwayFromZero);

        private static string BuildContextText(int uniqueViewers, double totalWatchHours, int starts)
        {
            if (uniqueViewers > 1)
            {
                return $"Watched by {uniqueViewers} users this week";
            }

            if (totalWatchHours >= 1D)
            {
                return $"{FormatWatchHours(totalWatchHours)} watched this week";
            }

            return $"{starts} start{(starts == 1 ? string.Empty : "s")} this week";
        }

        private static string FormatWatchHours(double value)
        {
            var rounded = value >= 10D
                ? Math.Round(value, 0, MidpointRounding.AwayFromZero)
                : Math.Round(value, 1, MidpointRounding.AwayFromZero);

            return rounded % 1 == 0
                ? $"{rounded:0}h"
                : $"{rounded:0.#}h";
        }

        private static IReadOnlyList<string> ParseGenres(string genres)
            => genres
                .Split(['|', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(genre => !string.IsNullOrWhiteSpace(genre))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToArray();

        private static TrendingPeriodDescriptor ResolvePeriod(TrendingNowPeriodType periodType, DateTime nowUtc)
        {
            var utcNow = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, _trendingTimeZone);

            return periodType switch
            {
                TrendingNowPeriodType.Week => CreatePeriodDescriptor(
                    periodType,
                    StartOfWeek(localNow.Date, DayOfWeek.Monday),
                    intervalDays: 7,
                    momentumDays: 2,
                    periodLabel: "This Week",
                    periodKeyPrefix: "W"),
                TrendingNowPeriodType.Month => CreatePeriodDescriptor(
                    periodType,
                    new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    intervalMonths: 1,
                    momentumDays: 7,
                    periodLabel: "This Month",
                    periodKeyPrefix: "M"),
                TrendingNowPeriodType.Season => CreatePeriodDescriptor(
                    periodType,
                    new DateTime(localNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    intervalYears: 1,
                    momentumDays: 30,
                    periodLabel: "This Season",
                    periodKeyPrefix: "S"),
                _ => throw new ArgumentOutOfRangeException(nameof(periodType))
            };
        }

        private static TrendingPeriodDescriptor CreatePeriodDescriptor(
            TrendingNowPeriodType periodType,
            DateTime localStart,
            int intervalDays = 0,
            int intervalMonths = 0,
            int intervalYears = 0,
            int momentumDays = 0,
            string periodLabel = "",
            string periodKeyPrefix = "")
        {
            var localEnd = intervalDays > 0
                ? localStart.AddDays(intervalDays)
                : intervalMonths > 0
                    ? localStart.AddMonths(intervalMonths)
                    : localStart.AddYears(intervalYears);
            var utcStart = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), _trendingTimeZone);
            var utcEnd = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localEnd, DateTimeKind.Unspecified), _trendingTimeZone);
            var momentumStart = utcEnd.AddDays(-Math.Max(1, momentumDays));
            if (momentumStart < utcStart)
            {
                momentumStart = utcStart;
            }

            var keyBody = periodType switch
            {
                TrendingNowPeriodType.Week => localStart.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                TrendingNowPeriodType.Month => localStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                TrendingNowPeriodType.Season => localStart.ToString("yyyy", CultureInfo.InvariantCulture),
                _ => localStart.ToString("O", CultureInfo.InvariantCulture)
            };

            return new TrendingPeriodDescriptor(
                $"{periodKeyPrefix}:{keyBody}",
                periodLabel,
                utcStart,
                utcEnd,
                momentumStart);
        }

        private static DateTime StartOfWeek(DateTime date, DayOfWeek startOfWeek)
        {
            var diff = (7 + (date.DayOfWeek - startOfWeek)) % 7;
            return date.AddDays(-diff);
        }

        private static TimeZoneInfo ResolveTrendingTimeZone()
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

        private sealed class AggregateRow
        {
            public Guid ItemId { get; set; }

            public long TotalValidatedTicks { get; set; }

            public int UniqueViewers { get; set; }

            public int Starts { get; set; }

            public int Completions { get; set; }

            public long MomentumTicks { get; set; }
        }

        private sealed class MetadataRow
        {
            public Guid ItemId { get; set; }

            public string ItemType { get; set; } = string.Empty;

            public string Title { get; set; } = string.Empty;

            public string Overview { get; set; } = string.Empty;

            public string Genres { get; set; } = string.Empty;

            public int? ProductionYear { get; set; }

            public long? RunTimeTicks { get; set; }

            public string OfficialRating { get; set; } = string.Empty;
        }

        private sealed class ImageRow
        {
            public Guid ItemId { get; set; }

            public ImageInfoImageType ImageType { get; set; }
        }

        private sealed record ImageSummary(bool HasPrimary, bool HasBackdrop);
    }

    /// <summary>
    /// Base candidate set for the OTT Trending pipeline.
    /// </summary>
    /// <param name="Period">Resolved period descriptor.</param>
    /// <param name="Candidates">Base candidates.</param>
    /// <param name="CandidateCount">Raw candidate count before the take limit.</param>
    /// <param name="UsedFallbackMode">Whether fallback behavior was needed due to low data.</param>
    public sealed record TrendingBaseCandidateSet(
        TrendingPeriodDescriptor Period,
        IReadOnlyList<TrendingBaseCandidate> Candidates,
        int CandidateCount,
        bool UsedFallbackMode);

    /// <summary>
    /// Base candidate row before personalization and editorial layers.
    /// </summary>
    public sealed class TrendingBaseCandidate
    {
        /// <summary>
        /// Gets or sets item id.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets item type.
        /// </summary>
        public string ItemType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets base score.
        /// </summary>
        public double BaseScore { get; set; }

        /// <summary>
        /// Gets or sets total watch hours.
        /// </summary>
        public double TotalWatchHours { get; set; }

        /// <summary>
        /// Gets or sets unique viewers.
        /// </summary>
        public int UniqueViewers { get; set; }

        /// <summary>
        /// Gets or sets starts.
        /// </summary>
        public int Starts { get; set; }

        /// <summary>
        /// Gets or sets completions.
        /// </summary>
        public int Completions { get; set; }

        /// <summary>
        /// Gets or sets momentum watch hours.
        /// </summary>
        public double MomentumWatchHours { get; set; }

        /// <summary>
        /// Gets or sets support copy.
        /// </summary>
        public string ContextText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets overview text.
        /// </summary>
        public string Overview { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets genres.
        /// </summary>
        public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets year.
        /// </summary>
        public int? ProductionYear { get; set; }

        /// <summary>
        /// Gets or sets runtime ticks.
        /// </summary>
        public long? RunTimeTicks { get; set; }

        /// <summary>
        /// Gets or sets official rating.
        /// </summary>
        public string OfficialRating { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether a primary image exists.
        /// </summary>
        public bool HasPrimaryImage { get; set; }

        /// <summary>
        /// Gets or sets whether a backdrop image exists.
        /// </summary>
        public bool HasBackdropImage { get; set; }
    }

    /// <summary>
    /// Shared resolved period descriptor for Trending calculations.
    /// </summary>
    /// <param name="PeriodKey">Period key.</param>
    /// <param name="PeriodLabel">Period label.</param>
    /// <param name="PeriodStartUtc">Period start in UTC.</param>
    /// <param name="PeriodEndUtc">Period end in UTC.</param>
    /// <param name="MomentumStartUtc">Momentum window start in UTC.</param>
    public sealed record TrendingPeriodDescriptor(
        string PeriodKey,
        string PeriodLabel,
        DateTime PeriodStartUtc,
        DateTime PeriodEndUtc,
        DateTime MomentumStartUtc);
}
