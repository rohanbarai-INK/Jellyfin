using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.UserDtos;

/// <summary>
/// The create user by name request body.
/// </summary>
public class CreateUserByName
{
    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    [Required]
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets an optional initial subscription duration in months.
    /// Supported values are 1, 3, 6 and 12.
    /// </summary>
    public int? InitialSubscriptionDurationMonths { get; set; }
}
