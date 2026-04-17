namespace Jellyfin.Api.Models.LeaderboardDtos;

/// <summary>
/// A neighboring competition row for the current user.
/// </summary>
public class LeaderboardCompetitionDto
{
    /// <summary>
    /// Gets or sets the leaderboard row entry.
    /// </summary>
    public LeaderboardEntryDto Entry { get; set; } = new();

    /// <summary>
    /// Gets or sets the metric gap to this entry.
    /// </summary>
    public long GapValue { get; set; }

    /// <summary>
    /// Gets or sets the formatted gap label.
    /// </summary>
    public string GapLabel { get; set; } = string.Empty;
}
