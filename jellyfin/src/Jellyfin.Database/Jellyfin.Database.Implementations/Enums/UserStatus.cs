namespace Jellyfin.Database.Implementations.Enums;

/// <summary>
/// User subscription status.
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// The user has active access.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The user access has expired.
    /// </summary>
    Expired = 1
}
