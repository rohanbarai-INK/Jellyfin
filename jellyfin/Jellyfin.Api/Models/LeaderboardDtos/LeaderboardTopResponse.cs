using System.Collections.Generic;

namespace Jellyfin.Api.Models.LeaderboardDtos;

/// <summary>
/// Top leaderboard response.
/// </summary>
public class LeaderboardTopResponse
{
    /// <summary>
    /// Gets or sets the season year.
    /// </summary>
    public int SeasonYear { get; set; }

    /// <summary>
    /// Gets or sets the metric type for this leaderboard page.
    /// </summary>
    public string MetricType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the leaderboard entries.
    /// </summary>
    public IReadOnlyList<LeaderboardEntryDto> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets the requesting user's position entry if not in top list.
    /// </summary>
    public LeaderboardEntryDto? CurrentUserPosition { get; set; }

    /// <summary>
    /// Gets or sets the total number of users in this season.
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Gets or sets the current page offset.
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// Gets or sets the requested page size.
    /// </summary>
    public int Limit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether more rows are available.
    /// </summary>
    public bool HasMore { get; set; }
}
