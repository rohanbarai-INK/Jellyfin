using System;
using System.Collections.Generic;

namespace MediaBrowser.Controller.Trending
{
    /// <summary>
    /// Represents a single Trending Now row.
    /// </summary>
    public sealed class TrendingNowItemResult
    {
        /// <summary>
        /// Gets or sets the target browseable item id.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the target item type.
        /// </summary>
        public string ItemType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target item title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the 1-based rank for the item.
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Gets or sets the neutral global trending score before viewer or admin boosts.
        /// </summary>
        public double BaseScore { get; set; }

        /// <summary>
        /// Gets or sets the personalization boost applied for the requesting user.
        /// </summary>
        public double PersonalizationBoost { get; set; }

        /// <summary>
        /// Gets or sets the editorial/admin boost applied to the item.
        /// </summary>
        public double AdminBoost { get; set; }

        /// <summary>
        /// Gets or sets the final score after all ranking layers.
        /// </summary>
        public double FinalScore { get; set; }

        /// <summary>
        /// Gets or sets total validated watch hours in the period.
        /// </summary>
        public double TotalWatchHours { get; set; }

        /// <summary>
        /// Gets or sets distinct validated viewers in the period.
        /// </summary>
        public int UniqueViewers { get; set; }

        /// <summary>
        /// Gets or sets validated starts in the period.
        /// </summary>
        public int Starts { get; set; }

        /// <summary>
        /// Gets or sets validated completion count in the period.
        /// </summary>
        public int Completions { get; set; }

        /// <summary>
        /// Gets or sets recent validated momentum watch hours.
        /// </summary>
        public double MomentumWatchHours { get; set; }

        /// <summary>
        /// Gets or sets an optional promotion id when this item is influenced by an admin promotion.
        /// </summary>
        public Guid? PromotionId { get; set; }

        /// <summary>
        /// Gets or sets the active editorial pin position, when present.
        /// </summary>
        public int? PinPosition { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the item is admin promoted.
        /// </summary>
        public bool IsAdminPromoted { get; set; }

        /// <summary>
        /// Gets or sets the primary UI label for the item.
        /// </summary>
        public string PrimaryLabel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the secondary badge when a second short badge is useful.
        /// </summary>
        public string SecondaryLabel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets explanation copy describing why the item is present.
        /// </summary>
        public string ExplanationText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the explanation source.
        /// </summary>
        public TrendingExplanationSource ExplanationSource { get; set; }

        /// <summary>
        /// Gets or sets a compact hero tagline or support line.
        /// </summary>
        public string Tagline { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional matched genre used for personalization messaging.
        /// </summary>
        public string MatchedGenre { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target audience segment matched by an admin promotion.
        /// </summary>
        public TrendingAudienceSegment? AudienceSegment { get; set; }

        /// <summary>
        /// Gets or sets the title overview text.
        /// </summary>
        public string Overview { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display genres.
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
        /// Gets or sets a value indicating whether the item has a primary/poster image.
        /// </summary>
        public bool HasPrimaryImage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the item has a backdrop image.
        /// </summary>
        public bool HasBackdropImage { get; set; }

        /// <summary>
        /// Gets or sets supporting UI text for the rail card.
        /// </summary>
        public string ContextText { get; set; } = string.Empty;
    }
}
