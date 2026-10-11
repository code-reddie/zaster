using System.Collections.Generic;
using Zaster.Accounts;
using Zaster.Database;

namespace Zaster.Users;

public sealed record User : Entity
{
    public required string Name { get; init; }

    public required string PasswordHash { get; init; }

    public List<Account> Accounts { get; init; } = [];

    /// <summary>
    /// Home-Assistant-Dienste (ohne „notify.“), über die der Nutzer Benachrichtigungen bekommt, z. B. <c>mobile_app_pixel_8</c>.
    /// </summary>
    public List<string> NotificationDevices { get; set; } = [];
}

/// <param name="CanDownloadBackup">Ob der Nutzer die Datenbank sichern darf (nur wer Zaster eingerichtet hat).</param>
public sealed record UserDto(int Id, string Name, bool CanDownloadBackup);
