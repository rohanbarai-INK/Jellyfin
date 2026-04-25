namespace Jellyfin.Api.Models.ActivityDtos;

/// <summary>
/// Request payload to update Trending Now settings.
/// </summary>
public class SetTrendingNowSettingsRequest
{
    /// <summary>
    /// Gets or sets maximum slides/items rendered in homepage Trending rail.
    /// </summary>
    public int MaxSlides { get; set; }
}
