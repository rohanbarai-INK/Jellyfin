namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// User state for admin drilldown endpoints.
/// </summary>
public enum AdminSubscriptionUserState
{
    /// <summary>
    /// Active subscription (expiry is null or in the future).
    /// </summary>
    Active,

    /// <summary>
    /// Grace window (expiry in the past but within configured grace period).
    /// </summary>
    Grace,

    /// <summary>
    /// Fully expired (expiry in the past and outside grace).
    /// </summary>
    Expired
}
