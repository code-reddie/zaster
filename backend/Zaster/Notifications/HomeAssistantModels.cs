using System.Collections.Generic;

namespace Zaster.Notifications;

/// <summary>
/// Stand der Benachrichtigungen für den angemeldeten Nutzer. Enthält nie den Token.
/// </summary>
public sealed record NotificationSettingsDto(
    bool HomeAssistantConfigured,
    bool ZasterUrlConfigured,
    IReadOnlyList<string> Devices);

public sealed record UpdateNotificationDevices(List<string> Devices);

/// <summary>
/// Ein Benachrichtigungsdienst aus Home Assistant, z. B. <c>mobile_app_pixel_8</c>.
/// </summary>
public sealed record HomeAssistantDevice(string Service, string Name);

public sealed record NotificationTestResult(IReadOnlyList<NotificationDeviceResult> Results);

public sealed record NotificationDeviceResult(string Device, bool Success, string? Error);
