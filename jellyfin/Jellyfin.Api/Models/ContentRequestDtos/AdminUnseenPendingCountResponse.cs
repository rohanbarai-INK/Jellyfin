namespace Jellyfin.Api.Models.ContentRequestDtos;

/// <summary>
/// Admin unseen pending count payload.
/// </summary>
public class AdminUnseenPendingCountResponse
{
    /// <summary>
    /// Gets or sets unseen pending row count.
    /// </summary>
    public int Count { get; set; }
}
