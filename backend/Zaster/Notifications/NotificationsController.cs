using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zaster.Common;
using Zaster.Database;

namespace Zaster.Notifications;

[Route("api/me/notifications")]
public sealed class NotificationsController(
    AppDbContext context,
    HomeAssistantClient homeAssistant,
    IOptions<HomeAssistantOptions> options) : ApiControllerBase
{
    private readonly HomeAssistantOptions _options = options.Value;

    /// <summary>
    /// Ob Home Assistant eingerichtet ist und an welche Geräte der Nutzer Benachrichtigungen bekommt.
    /// </summary>
    [HttpGet]
    public async Task<NotificationSettingsDto> GetSettings(CancellationToken cancellationToken)
    {
        var devices = await context.Users
            .Where(u => u.Id == UserId)
            .Select(u => u.NotificationDevices)
            .SingleAsync(cancellationToken);
        return ToDto(devices);
    }

    /// <summary>
    /// Speichert die Geräte (notify-Dienste ohne „notify.“), an die der Nutzer Benachrichtigungen bekommt.
    /// </summary>
    [HttpPut("devices")]
    public async Task<ActionResult<NotificationSettingsDto>> UpdateDevices(
        UpdateNotificationDevices request,
        CancellationToken cancellationToken)
    {
        var devices = request.Devices
            .Select(d => d.Trim())
            .Select(d => d.StartsWith("notify.", StringComparison.Ordinal) ? d["notify.".Length..] : d)
            .Where(d => d.Length > 0)
            .Distinct()
            .ToList();

        var invalid = devices.FirstOrDefault(d => !HomeAssistantClient.IsValidServiceName(d));
        if (invalid is not null)
        {
            return BadRequestProblem($"„{invalid}“ ist kein gültiger Dienstname. Erlaubt sind Kleinbuchstaben, Ziffern und _, z. B. mobile_app_pixel_8.");
        }

        var user = await context.Users.SingleAsync(u => u.Id == UserId, cancellationToken);
        user.NotificationDevices = devices;
        await context.SaveChangesAsync(cancellationToken);

        return ToDto(user.NotificationDevices);
    }

    /// <summary>
    /// Liest die Geräte mit Home-Assistant-App (Dienste notify.mobile_app_*) aus Home Assistant.
    /// </summary>
    [HttpGet("available-devices")]
    public async Task<ActionResult<IReadOnlyList<HomeAssistantDevice>>> GetAvailableDevices(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await homeAssistant.GetMobileAppDevicesAsync(cancellationToken));
        }
        catch (HomeAssistantException e)
        {
            return BadRequestProblem(e.Message);
        }
    }

    /// <summary>
    /// Schickt eine Testnachricht an alle gespeicherten Geräte des Nutzers. Ein Tipp darauf öffnet Zaster.
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult<NotificationTestResult>> SendTest(CancellationToken cancellationToken)
    {
        if (!homeAssistant.IsConfigured)
        {
            return BadRequestProblem("Home Assistant ist nicht eingerichtet. Bitte HomeAssistant__Url und HomeAssistant__Token in der Konfiguration setzen.");
        }

        if (!HomeAssistantClient.TryParseHttpUri(_options.ZasterUrl, out var zasterUrl))
        {
            return BadRequestProblem("Die Adresse von Zaster fehlt. Bitte HomeAssistant__ZasterUrl in der Konfiguration setzen (so, wie das Handy Zaster erreicht).");
        }

        var devices = await context.Users
            .Where(u => u.Id == UserId)
            .Select(u => u.NotificationDevices)
            .SingleAsync(cancellationToken);
        if (devices.Count == 0)
        {
            return BadRequestProblem("Bitte zuerst mindestens ein Gerät auswählen und speichern.");
        }

        var results = new List<NotificationDeviceResult>();
        foreach (var device in devices)
        {
            try
            {
                await homeAssistant.SendNotificationAsync(
                    device,
                    "Zaster",
                    "Testnachricht: Benachrichtigungen funktionieren. Tippe hier, um Zaster zu öffnen.",
                    zasterUrl,
                    cancellationToken);
                results.Add(new NotificationDeviceResult(device, true, null));
            }
            catch (HomeAssistantException e)
            {
                results.Add(new NotificationDeviceResult(device, false, e.Message));
            }
        }

        return new NotificationTestResult(results);
    }

    private NotificationSettingsDto ToDto(IReadOnlyList<string> devices) => new(
        homeAssistant.IsConfigured,
        HomeAssistantClient.TryParseHttpUri(_options.ZasterUrl, out _),
        devices);
}
