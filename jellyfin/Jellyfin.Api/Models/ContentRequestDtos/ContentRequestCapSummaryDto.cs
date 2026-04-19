using System;

namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Request cap summary for the current subscription cycle.
/// </summary>
public class ContentRequestCapSummaryDto
{
    /// <summary>
    /// Gets or sets cycle start date.
    /// </summary>
    public DateTime CycleStartDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether subscription is active.
    /// </summary>
    public bool IsSubscriptionActive { get; set; }

    /// <summary>
    /// Gets or sets movie cap.
    /// </summary>
    public int MovieCap { get; set; }

    /// <summary>
    /// Gets or sets series cap.
    /// </summary>
    public int SeriesCap { get; set; }

    /// <summary>
    /// Gets or sets used movie slots.
    /// </summary>
    public int UsedMovies { get; set; }

    /// <summary>
    /// Gets or sets used series slots.
    /// </summary>
    public int UsedSeries { get; set; }

    /// <summary>
    /// Gets or sets remaining movie slots.
    /// </summary>
    public int RemainingMovies { get; set; }

    /// <summary>
    /// Gets or sets remaining series slots.
    /// </summary>
    public int RemainingSeries { get; set; }

    /// <summary>
    /// Gets or sets currently available rewarded movie slots.
    /// </summary>
    public int RewardMovies { get; set; }

    /// <summary>
    /// Gets or sets currently available rewarded series slots.
    /// </summary>
    public int RewardSeries { get; set; }
}
