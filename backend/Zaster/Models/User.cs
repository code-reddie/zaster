using System.Collections.Generic;

namespace Zaster.Models;

public sealed record User : Entity
{
    public required string Name { get; init; }

    public required string PasswordHash { get; init; }

    public List<Account> Accounts { get; init; } = [];

    public List<Category> Categories { get; init; } = [];

    /// <summary>
    /// Home-Assistant-Dienste (ohne „notify.“), über die der Nutzer Benachrichtigungen bekommt, z. B. <c>mobile_app_pixel_8</c>.
    /// </summary>
    public List<string> NotificationDevices { get; set; } = [];
}

public sealed record UserDto(int Id, string Name);
