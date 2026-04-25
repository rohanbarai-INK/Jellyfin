namespace Jellyfin.Api.Models.ActivityDtos;

/// <summary>
/// Trending Now rail settings dto.
/// </summary>
public class TrendingNowSettingsDto
{
    /// <summary>
    /// Gets or sets the maximum slides/items rendered in homepage Trending rail.
    /// </summary>
    public int MaxSlides { get; set; }
}
