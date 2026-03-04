namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Admin subscription cohort row.
/// </summary>
public class AdminSubscriptionCohortResponse
{
    /// <summary>
    /// Gets or sets month label.
    /// </summary>
    public required string Month { get; set; }

    /// <summary>
    /// Gets or sets users joined in cohort month.
    /// </summary>
    public int UsersJoined { get; set; }

    /// <summary>
    /// Gets or sets renewal percentage.
    /// </summary>
    public int RenewalRate { get; set; }
}
