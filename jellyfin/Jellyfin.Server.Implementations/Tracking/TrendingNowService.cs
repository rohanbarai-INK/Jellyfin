using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Trending;

namespace Jellyfin.Server.Implementations.Tracking
{
    /// <inheritdoc />
    public class TrendingNowService : ITrendingNowService
    {
        private readonly TrendingBaseService _trendingBaseService;
        private readonly TrendingPersonalizationService _trendingPersonalizationService;
        private readonly ITrendingPromotionService _trendingPromotionService;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrendingNowService"/> class.
        /// </summary>
        /// <param name="trendingBaseService">Base trending service.</param>
        /// <param name="trendingPersonalizationService">Personalization service.</param>
        /// <param name="trendingPromotionService">Promotion service.</param>
        /// <param name="timeProvider">Time provider.</param>
        public TrendingNowService(
            TrendingBaseService trendingBaseService,
            TrendingPersonalizationService trendingPersonalizationService,
            ITrendingPromotionService trendingPromotionService,
            TimeProvider timeProvider)
        {
            _trendingBaseService = trendingBaseService;
            _trendingPersonalizationService = trendingPersonalizationService;
            _trendingPromotionService = trendingPromotionService;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc />
        public async Task<TrendingNowResult> GetTrendingNow(Guid requestingUserId, TrendingNowPeriodType periodType, int limit)
        {
            if (requestingUserId == Guid.Empty)
            {
                throw new ArgumentException("Requesting user id cannot be empty.", nameof(requestingUserId));
            }

            var normalizedLimit = Math.Clamp(limit, 1, 30);
            var candidateLimit = Math.Clamp(Math.Max(normalizedLimit * 4, 24), normalizedLimit, 80);
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

            var baseCandidateSet = await _trendingBaseService
                .GetBaseCandidates(periodType, candidateLimit)
                .ConfigureAwait(false);

            var snapshotTask = _trendingPersonalizationService.BuildSnapshot(requestingUserId);
            var promotionsTask = _trendingPromotionService.GetActivePromotions(nowUtc);

            await Task.WhenAll(snapshotTask, promotionsTask).ConfigureAwait(false);

            var snapshot = await snapshotTask.ConfigureAwait(false);
            var promotions = await promotionsTask.ConfigureAwait(false);
            var applicablePromotions = promotions
                .Where(promotion => _trendingPersonalizationService.MatchesAudience(snapshot, promotion.AudienceSegment, promotion.AudienceValue))
                .ToList();

            var candidates = baseCandidateSet.Candidates
                .Select(candidate => new RankedCandidate(candidate))
                .ToDictionary(candidate => candidate.ItemId);

            var missingPromotionItemIds = applicablePromotions
                .Select(promotion => promotion.ItemId)
                .Where(itemId => !candidates.ContainsKey(itemId))
                .Distinct()
                .ToArray();

            if (missingPromotionItemIds.Length > 0)
            {
                var forcedCandidates = await _trendingBaseService
                    .GetCandidatesByIds(missingPromotionItemIds)
                    .ConfigureAwait(false);

                foreach (var forcedCandidate in forcedCandidates)
                {
                    candidates.TryAdd(forcedCandidate.ItemId, new RankedCandidate(forcedCandidate));
                }
            }

            foreach (var rankedCandidate in candidates.Values)
            {
                rankedCandidate.Personalization = _trendingPersonalizationService.ScoreCandidate(rankedCandidate.BaseCandidate, snapshot);
                rankedCandidate.ApplicablePromotions = applicablePromotions
                    .Where(promotion => promotion.ItemId.Equals(rankedCandidate.ItemId))
                    .OrderBy(promotion => promotion.PinPosition ?? int.MaxValue)
                    .ThenByDescending(promotion => promotion.BoostAmount)
                    .ThenByDescending(promotion => promotion.UpdatedAtUtc)
                    .ToList();
                rankedCandidate.AdminBoost = Math.Round(
                    rankedCandidate.ApplicablePromotions.Sum(promotion => promotion.BoostAmount),
                    2,
                    MidpointRounding.AwayFromZero);
                rankedCandidate.PinPosition = rankedCandidate.ApplicablePromotions
                    .Where(promotion => promotion.PinPosition.HasValue && promotion.PinPosition.Value > 0)
                    .Select(promotion => promotion.PinPosition)
                    .DefaultIfEmpty(null)
                    .Min();
                rankedCandidate.PrimaryPromotion = rankedCandidate.ApplicablePromotions
                    .OrderBy(promotion => promotion.PinPosition ?? int.MaxValue)
                    .ThenByDescending(promotion => promotion.BoostAmount)
                    .ThenByDescending(promotion => promotion.UpdatedAtUtc)
                    .FirstOrDefault();
                rankedCandidate.FinalScore = Math.Round(
                    rankedCandidate.BaseCandidate.BaseScore + rankedCandidate.Personalization.Boost + rankedCandidate.AdminBoost,
                    2,
                    MidpointRounding.AwayFromZero);
            }

            var orderedItems = candidates.Values
                .OrderBy(candidate => candidate.PinPosition ?? int.MaxValue)
                .ThenByDescending(candidate => candidate.FinalScore)
                .ThenByDescending(candidate => candidate.BaseCandidate.BaseScore)
                .ThenByDescending(candidate => candidate.BaseCandidate.MomentumWatchHours)
                .ThenByDescending(candidate => candidate.BaseCandidate.UniqueViewers)
                .ThenBy(candidate => candidate.BaseCandidate.Title, StringComparer.OrdinalIgnoreCase)
                .Take(normalizedLimit)
                .ToList();

            var items = new List<TrendingNowItemResult>(orderedItems.Count);
            for (var index = 0; index < orderedItems.Count; index++)
            {
                items.Add(ToResult(orderedItems[index], index + 1, baseCandidateSet.Period.PeriodLabel));
            }

            return new TrendingNowResult
            {
                PeriodKey = baseCandidateSet.Period.PeriodKey,
                PeriodLabel = baseCandidateSet.Period.PeriodLabel,
                PeriodStartUtc = baseCandidateSet.Period.PeriodStartUtc,
                PeriodEndUtc = baseCandidateSet.Period.PeriodEndUtc,
                Limit = normalizedLimit,
                CandidateCount = Math.Max(baseCandidateSet.CandidateCount, candidates.Count),
                UsedFallbackMode = baseCandidateSet.UsedFallbackMode || (baseCandidateSet.CandidateCount == 0 && items.Count > 0),
                Items = items
            };
        }

        private static TrendingNowItemResult ToResult(RankedCandidate candidate, int rank, string periodLabel)
        {
            var baseCandidate = candidate.BaseCandidate;
            var primaryLabel = BuildPrimaryLabel(candidate, rank);
            var secondaryLabel = BuildSecondaryLabel(candidate, rank);
            var explanationSource = ResolveExplanationSource(candidate);
            var explanationText = BuildExplanationText(candidate, periodLabel, rank);
            var tagline = BuildTagline(candidate, periodLabel);

            return new TrendingNowItemResult
            {
                ItemId = baseCandidate.ItemId,
                ItemType = baseCandidate.ItemType,
                Title = baseCandidate.Title,
                Rank = rank,
                BaseScore = baseCandidate.BaseScore,
                PersonalizationBoost = candidate.Personalization.Boost,
                AdminBoost = candidate.AdminBoost,
                FinalScore = candidate.FinalScore,
                TotalWatchHours = baseCandidate.TotalWatchHours,
                UniqueViewers = baseCandidate.UniqueViewers,
                Starts = baseCandidate.Starts,
                Completions = baseCandidate.Completions,
                MomentumWatchHours = baseCandidate.MomentumWatchHours,
                PromotionId = candidate.PrimaryPromotion?.Id,
                PinPosition = candidate.PinPosition,
                IsAdminPromoted = candidate.PrimaryPromotion is not null,
                PrimaryLabel = primaryLabel,
                SecondaryLabel = secondaryLabel,
                ExplanationText = explanationText,
                ExplanationSource = explanationSource,
                Tagline = tagline,
                MatchedGenre = candidate.Personalization.MatchedGenre,
                AudienceSegment = candidate.PrimaryPromotion?.AudienceSegment,
                Overview = baseCandidate.Overview,
                Genres = baseCandidate.Genres,
                ProductionYear = baseCandidate.ProductionYear,
                RunTimeTicks = baseCandidate.RunTimeTicks,
                OfficialRating = baseCandidate.OfficialRating,
                HasPrimaryImage = baseCandidate.HasPrimaryImage,
                HasBackdropImage = baseCandidate.HasBackdropImage,
                ContextText = baseCandidate.ContextText
            };
        }

        private static TrendingExplanationSource ResolveExplanationSource(RankedCandidate candidate)
        {
            if (candidate.PrimaryPromotion is not null)
            {
                return TrendingExplanationSource.AdminPromotion;
            }

            if (candidate.Personalization.Boost > 0D)
            {
                return TrendingExplanationSource.Personalization;
            }

            return TrendingExplanationSource.BaseTrending;
        }

        private static string BuildPrimaryLabel(RankedCandidate candidate, int rank)
        {
            var overrideLabel = candidate.ApplicablePromotions
                .Select(promotion => promotion.LabelOverride)
                .FirstOrDefault(label => !string.IsNullOrWhiteSpace(label));
            if (!string.IsNullOrWhiteSpace(overrideLabel))
            {
                return overrideLabel.Trim();
            }

            if (candidate.PrimaryPromotion is not null)
            {
                return candidate.PinPosition.HasValue ? "Featured" : "Editor's Pick";
            }

            if (candidate.Personalization.Boost > 0D)
            {
                return "Recommended for You";
            }

            return rank switch
            {
                1 => "#Trending #1",
                2 => "Hot #2",
                3 => "Hot #3",
                _ => "Trending Now"
            };
        }

        private static string BuildSecondaryLabel(RankedCandidate candidate, int rank)
        {
            if (candidate.PrimaryPromotion is not null && rank <= 3 && candidate.BaseCandidate.BaseScore > 0D)
            {
                return rank switch
                {
                    1 => "#1 This Week",
                    2 => "#2 This Week",
                    3 => "#3 This Week",
                    _ => string.Empty
                };
            }

            if (candidate.Personalization.Boost > 0D && !string.IsNullOrWhiteSpace(candidate.Personalization.MatchedGenre))
            {
                return $"Trending in {candidate.Personalization.MatchedGenre}";
            }

            return string.Empty;
        }

        private static string BuildExplanationText(RankedCandidate candidate, string periodLabel, int rank)
        {
            if (candidate.PrimaryPromotion is not null)
            {
                var audienceExplanation = BuildAudienceExplanation(candidate.PrimaryPromotion);
                return !string.IsNullOrWhiteSpace(candidate.PrimaryPromotion.LabelOverride)
                    ? candidate.PrimaryPromotion.LabelOverride
                    : audienceExplanation;
            }

            if (!string.IsNullOrWhiteSpace(candidate.Personalization.ExplanationText))
            {
                return candidate.Personalization.ExplanationText;
            }

            return rank switch
            {
                1 => $"{periodLabel} front-runner",
                <= 3 => $"Popular {periodLabel.ToLowerInvariant()}",
                _ => $"Trending {periodLabel.ToLowerInvariant()}"
            };
        }

        private static string BuildAudienceExplanation(TrendingPromotionInfo promotion)
        {
            return promotion.AudienceSegment switch
            {
                TrendingAudienceSegment.AllUsers => "Featured by KnightFlix",
                TrendingAudienceSegment.NewOrLowHistory => "Featured for new viewers",
                TrendingAudienceSegment.ReturningUsers => "Featured for returning viewers",
                TrendingAudienceSegment.MovieHeavy => "Featured for movie fans",
                TrendingAudienceSegment.SeriesHeavy => "Featured for series fans",
                TrendingAudienceSegment.TopGenreMatch when !string.IsNullOrWhiteSpace(promotion.AudienceValue) => $"Trending in {promotion.AudienceValue}",
                _ => "Featured by KnightFlix"
            };
        }

        private static string BuildTagline(RankedCandidate candidate, string periodLabel)
        {
            var overrideTagline = candidate.ApplicablePromotions
                .Select(promotion => promotion.TaglineOverride)
                .FirstOrDefault(tagline => !string.IsNullOrWhiteSpace(tagline));
            if (!string.IsNullOrWhiteSpace(overrideTagline))
            {
                return overrideTagline.Trim();
            }

            if (candidate.BaseCandidate.UniqueViewers > 1)
            {
                return $"Watched by {candidate.BaseCandidate.UniqueViewers} users {periodLabel.ToLowerInvariant()}";
            }

            if (candidate.BaseCandidate.TotalWatchHours >= 1D)
            {
                return $"{FormatWatchHours(candidate.BaseCandidate.TotalWatchHours)} watched {periodLabel.ToLowerInvariant()}";
            }

            if (candidate.PrimaryPromotion is not null)
            {
                return "Featured across KnightFlix";
            }

            return candidate.BaseCandidate.ContextText;
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

        private sealed class RankedCandidate
        {
            public RankedCandidate(TrendingBaseCandidate baseCandidate)
            {
                BaseCandidate = baseCandidate;
            }

            public Guid ItemId => BaseCandidate.ItemId;

            public TrendingBaseCandidate BaseCandidate { get; }

            public TrendingPersonalizationResult Personalization { get; set; } = TrendingPersonalizationResult.Empty;

            public IReadOnlyList<TrendingPromotionInfo> ApplicablePromotions { get; set; } = Array.Empty<TrendingPromotionInfo>();

            public TrendingPromotionInfo? PrimaryPromotion { get; set; }

            public double AdminBoost { get; set; }

            public int? PinPosition { get; set; }

            public double FinalScore { get; set; }
        }
    }
}
