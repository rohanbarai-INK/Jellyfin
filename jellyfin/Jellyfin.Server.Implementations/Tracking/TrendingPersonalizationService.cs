using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Enums;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Server.Implementations.Tracking
{
    /// <summary>
    /// Applies viewer-affinity boosts to the neutral Trending candidate set.
    /// </summary>
    public class TrendingPersonalizationService
    {
        private const string AllTimeKey = "ALL";

        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrendingPersonalizationService"/> class.
        /// </summary>
        /// <param name="dbProvider">Database provider.</param>
        public TrendingPersonalizationService(IDbContextFactory<JellyfinDbContext> dbProvider)
        {
            _dbProvider = dbProvider;
        }

        /// <summary>
        /// Builds the viewer personalization snapshot.
        /// </summary>
        /// <param name="userId">Requesting user id.</param>
        /// <returns>Snapshot for ranking and audience matching.</returns>
        public async Task<TrendingPersonalizationSnapshot> BuildSnapshot(Guid userId)
        {
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var allTimeStats = await dbContext.UserPeriodStats
                    .AsNoTracking()
                    .FirstOrDefaultAsync(stats => stats.UserId.Equals(userId)
                        && stats.PeriodType == PeriodType.AllTime
                        && stats.PeriodKey == AllTimeKey)
                    .ConfigureAwait(false);

                var topGenres = await dbContext.UserGenrePeriodStats
                    .AsNoTracking()
                    .Where(stats => stats.UserId.Equals(userId)
                        && stats.PeriodType == PeriodType.AllTime
                        && stats.PeriodKey == AllTimeKey)
                    .OrderByDescending(stats => stats.TotalValidatedTicks)
                    .Take(3)
                    .ToListAsync()
                    .ConfigureAwait(false);

                var recentCutoffUtc = DateTime.UtcNow.AddDays(-90);
                var recentDistribution = await (
                        from session in dbContext.UserWatchSessions.AsNoTracking()
                        join item in dbContext.BaseItems.AsNoTracking() on session.ItemId equals item.Id
                        where session.UserId.Equals(userId)
                            && session.IsValidSession
                            && session.ValidatedTicks > 0
                            && session.StartTimeUtc >= recentCutoffUtc
                        select new
                        {
                            item.Type,
                            session.ValidatedTicks
                        })
                    .ToListAsync()
                    .ConfigureAwait(false);

                var continueWatchingCount = await dbContext.UserData
                    .AsNoTracking()
                    .CountAsync(row => row.UserId.Equals(userId)
                        && row.PlaybackPositionTicks > 0
                        && !row.Played)
                    .ConfigureAwait(false);

                var recentBingeCount = await dbContext.UserBingeSessions
                    .AsNoTracking()
                    .CountAsync(row => row.UserId.Equals(userId)
                        && row.SessionDateUtc >= recentCutoffUtc)
                    .ConfigureAwait(false);

                var totalSessions = allTimeStats?.SessionCount ?? 0;
                var completedTitles = (allTimeStats?.CompletedMovies ?? 0) + (allTimeStats?.CompletedEpisodes ?? 0);
                var completionRatio = totalSessions > 0 ? (double)completedTitles / totalSessions : 0D;
                var movieTicks = recentDistribution
                    .Where(row => IsMovieType(row.Type))
                    .Sum(row => row.ValidatedTicks);
                var seriesTicks = recentDistribution
                    .Where(row => IsSeriesType(row.Type))
                    .Sum(row => row.ValidatedTicks);
                var totalRecentTicks = Math.Max(1L, movieTicks + seriesTicks);
                var movieShare = movieTicks / (double)totalRecentTicks;
                var seriesShare = seriesTicks / (double)totalRecentTicks;
                var genreWeights = topGenres.Count == 0
                    ? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    : topGenres
                        .Select((genre, index) => new
                        {
                            genre.GenreId,
                            Weight = index switch
                            {
                                0 => 1D,
                                1 => 0.75D,
                                _ => 0.55D
                            }
                        })
                        .ToDictionary(row => row.GenreId, row => row.Weight, StringComparer.OrdinalIgnoreCase);

                return new TrendingPersonalizationSnapshot(
                    totalSessions,
                    totalSessions < 5,
                    movieShare,
                    seriesShare,
                    completionRatio,
                    continueWatchingCount,
                    recentBingeCount,
                    genreWeights);
            }
        }

        /// <summary>
        /// Scores a single base candidate for the supplied viewer.
        /// </summary>
        /// <param name="candidate">Base candidate.</param>
        /// <param name="snapshot">Viewer snapshot.</param>
        /// <returns>Personalization result.</returns>
        public TrendingPersonalizationResult ScoreCandidate(TrendingBaseCandidate candidate, TrendingPersonalizationSnapshot snapshot)
        {
            if (snapshot.IsLowHistory || snapshot.TotalSessions <= 0)
            {
                return TrendingPersonalizationResult.Empty;
            }

            var baseScore = Math.Max(candidate.BaseScore, 1D);
            var boost = 0D;
            var matchedGenre = string.Empty;
            var explanationText = string.Empty;

            foreach (var genre in candidate.Genres)
            {
                if (!snapshot.TopGenreWeights.TryGetValue(genre, out var weight))
                {
                    continue;
                }

                matchedGenre = genre;
                boost += baseScore * (0.08D * weight);
                explanationText = $"Because you watch {genre}";
                break;
            }

            if (IsMovieType(candidate.ItemType) && snapshot.MovieShare >= 0.6D)
            {
                boost += baseScore * 0.08D;
                explanationText = string.IsNullOrWhiteSpace(explanationText) ? "Recommended for your category mix" : explanationText;
            }

            if (IsSeriesType(candidate.ItemType) && snapshot.SeriesShare >= 0.6D)
            {
                boost += baseScore * 0.08D;
                explanationText = string.IsNullOrWhiteSpace(explanationText) ? "Recommended for your category mix" : explanationText;
            }

            if (IsSeriesType(candidate.ItemType) && snapshot.RecentBingeCount > 0)
            {
                boost += baseScore * 0.04D;
            }

            if (snapshot.CompletionRatio >= 0.55D && candidate.Completions > 0)
            {
                boost += baseScore * 0.03D;
            }

            if (snapshot.ContinueWatchingCount > 0 && IsSeriesType(candidate.ItemType))
            {
                boost += baseScore * 0.02D;
            }

            var cappedBoost = Math.Round(Math.Min(boost, baseScore * 0.35D), 2, MidpointRounding.AwayFromZero);
            if (cappedBoost <= 0D)
            {
                return TrendingPersonalizationResult.Empty;
            }

            return new TrendingPersonalizationResult(
                cappedBoost,
                matchedGenre,
                string.IsNullOrWhiteSpace(explanationText) ? "Recommended for your category mix" : explanationText);
        }

        /// <summary>
        /// Determines if the target snapshot matches a configured audience segment.
        /// </summary>
        /// <param name="snapshot">Viewer snapshot.</param>
        /// <param name="segment">Audience segment.</param>
        /// <param name="audienceValue">Optional audience value.</param>
        /// <returns><c>true</c> when matched.</returns>
        public bool MatchesAudience(TrendingPersonalizationSnapshot snapshot, MediaBrowser.Controller.Trending.TrendingAudienceSegment segment, string audienceValue)
        {
            return segment switch
            {
                MediaBrowser.Controller.Trending.TrendingAudienceSegment.AllUsers => true,
                MediaBrowser.Controller.Trending.TrendingAudienceSegment.NewOrLowHistory => snapshot.IsLowHistory,
                MediaBrowser.Controller.Trending.TrendingAudienceSegment.ReturningUsers => !snapshot.IsLowHistory && snapshot.TotalSessions >= 5,
                MediaBrowser.Controller.Trending.TrendingAudienceSegment.MovieHeavy => snapshot.MovieShare >= 0.6D,
                MediaBrowser.Controller.Trending.TrendingAudienceSegment.SeriesHeavy => snapshot.SeriesShare >= 0.6D,
                MediaBrowser.Controller.Trending.TrendingAudienceSegment.TopGenreMatch => !string.IsNullOrWhiteSpace(audienceValue)
                    && snapshot.TopGenreWeights.ContainsKey(audienceValue.Trim()),
                _ => true
            };
        }

        private static bool IsMovieType(string type)
            => type.Contains("Movie", StringComparison.OrdinalIgnoreCase);

        private static bool IsSeriesType(string type)
            => type.Contains("Series", StringComparison.OrdinalIgnoreCase)
                || type.Contains("Episode", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Personalization snapshot for a requesting viewer.
    /// </summary>
    /// <param name="TotalSessions">Total validated sessions known for the user.</param>
    /// <param name="IsLowHistory">Whether the user lacks enough history for confident personalization.</param>
    /// <param name="MovieShare">Movie watch share.</param>
    /// <param name="SeriesShare">Series watch share.</param>
    /// <param name="CompletionRatio">Completion tendency ratio.</param>
    /// <param name="ContinueWatchingCount">Continue-watching item count.</param>
    /// <param name="RecentBingeCount">Recent binge-session count.</param>
    /// <param name="TopGenreWeights">Weighted top-genre map.</param>
    public sealed record TrendingPersonalizationSnapshot(
        int TotalSessions,
        bool IsLowHistory,
        double MovieShare,
        double SeriesShare,
        double CompletionRatio,
        int ContinueWatchingCount,
        int RecentBingeCount,
        IReadOnlyDictionary<string, double> TopGenreWeights);

    /// <summary>
    /// Result of personalizing a base candidate.
    /// </summary>
    /// <param name="Boost">Applied personalization boost.</param>
    /// <param name="MatchedGenre">Matched genre, when available.</param>
    /// <param name="ExplanationText">Explanation text to show in the UI.</param>
    public sealed record TrendingPersonalizationResult(
        double Boost,
        string MatchedGenre,
        string ExplanationText)
    {
        /// <summary>
        /// Gets an empty personalization result.
        /// </summary>
        public static TrendingPersonalizationResult Empty { get; } = new(0D, string.Empty, string.Empty);
    }
}
