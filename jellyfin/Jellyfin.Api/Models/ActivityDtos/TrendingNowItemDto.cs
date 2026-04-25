using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Trending;

namespace Jellyfin.Api.Models.ActivityDtos
{
    /// <summary>
    /// Trending Now item response.
    /// </summary>
    public sealed class TrendingNowItemDto
    {
        /// <summary>
        /// Gets or sets the browseable item id.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item type.
        /// </summary>
        public string ItemType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the item title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the 1-based rank.
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Gets or sets the neutral global trending score.
        /// </summary>
        public double BaseScore { get; set; }

        /// <summary>
        /// Gets or sets the personalization boost.
        /// </summary>
        public double PersonalizationBoost { get; set; }

        /// <summary>
        /// Gets or sets the admin/editorial boost.
        /// </summary>
        public double AdminBoost { get; set; }

        /// <summary>
        /// Gets or sets the final merged score.
        /// </summary>
        public double FinalScore { get; set; }

        /// <summary>
        /// Gets or sets validated watch hours.
        /// </summary>
        public double TotalWatchHours { get; set; }

        /// <summary>
        /// Gets or sets unique viewers.
        /// </summary>
        public int UniqueViewers { get; set; }

        /// <summary>
        /// Gets or sets validated starts.
        /// </summary>
        public int Starts { get; set; }

        /// <summary>
        /// Gets or sets completion count.
        /// </summary>
        public int Completions { get; set; }

        /// <summary>
        /// Gets or sets recent momentum watch hours.
        /// </summary>
        public double MomentumWatchHours { get; set; }

        /// <summary>
        /// Gets or sets the related promotion id when present.
        /// </summary>
        public Guid? PromotionId { get; set; }

        /// <summary>
        /// Gets or sets the active pin position when present.
        /// </summary>
        public int? PinPosition { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the item is admin promoted.
        /// </summary>
        public bool IsAdminPromoted { get; set; }

        /// <summary>
        /// Gets or sets the primary label.
        /// </summary>
        public string PrimaryLabel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional secondary label.
        /// </summary>
        public string SecondaryLabel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets explanation copy.
        /// </summary>
        public string ExplanationText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the explanation source.
        /// </summary>
        public TrendingExplanationSource ExplanationSource { get; set; }

        /// <summary>
        /// Gets or sets a hero tagline.
        /// </summary>
        public string Tagline { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a matched genre when applicable.
        /// </summary>
        public string MatchedGenre { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the matched audience segment when applicable.
        /// </summary>
        public TrendingAudienceSegment? AudienceSegment { get; set; }

        /// <summary>
        /// Gets or sets the overview text.
        /// </summary>
        public string Overview { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the genre list.
        /// </summary>
        public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the production year.
        /// </summary>
        public int? ProductionYear { get; set; }

        /// <summary>
        /// Gets or sets the runtime ticks.
        /// </summary>
        public long? RunTimeTicks { get; set; }

        /// <summary>
        /// Gets or sets the official rating.
        /// </summary>
        public string OfficialRating { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether primary/poster artwork exists.
        /// </summary>
        public bool HasPrimaryImage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether backdrop artwork exists.
        /// </summary>
        public bool HasBackdropImage { get; set; }

        /// <summary>
        /// Gets or sets UI context text for the card.
        /// </summary>
        public string ContextText { get; set; } = string.Empty;
    }
}
