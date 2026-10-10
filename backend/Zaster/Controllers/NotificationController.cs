using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Zaster.Database;
using Zaster.Notifications;

namespace Zaster.Controllers;

[ApiController]
[Route("api/notification")]
public sealed class NotificationController(
    AppDbContext context,
    HomeAssistantClient homeAssistant,
    IOptions<HomeAssistantOptions> options) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly HomeAssistantClient _homeAssistant = homeAssistant;
    private readonly HomeAssistantOptions _options = options.Value;

    private int? GetUserId()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdString, out var userId) ? userId : null;
    }

    /// <summary>
    /// Ob Home Assistant eingerichtet ist und an welche Geräte der Nutzer Benachrichtigungen bekommt.
    /// </summary>
    [HttpGet("settings")]
    public async Task<ActionResult<NotificationSettingsDto>> GetSettings(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(ToDto(user.NotificationDevices));
    }

    /// <summary>
    /// Speichert die Geräte (notify-Dienste ohne „notify.“), an die der Nutzer Benachrichtigungen bekommt.
    /// </summary>
    [HttpPut("devices")]
    public async Task<ActionResult<NotificationSettingsDto>> UpdateDevices(
        UpdateNotificationDevices request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            return Unauthorized();
        }

        var devices = request.Devices
            .Select(d => d.Trim())
            .Select(d => d.StartsWith("notify.", StringComparison.Ordinal) ? d["notify.".Length..] : d)
            .Where(d => d.Length > 0)
            .Distinct()
            .ToList();

        var invalid = devices.FirstOrDefault(d => !HomeAssistantClient.IsValidServiceName(d));
        if (invalid is not null)
        {
            return BadRequest($"„{invalid}“ ist kein gültiger Dienstname. Erlaubt sind Kleinbuchstaben, Ziffern und _, z. B. mobile_app_pixel_8.");
        }

        user.NotificationDevices = devices;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(user.NotificationDevices));
    }

    /// <summary>
    /// Liest die Geräte mit Home-Assistant-App (Dienste notify.mobile_app_*) aus Home Assistant.
    /// </summary>
    [HttpGet("available-devices")]
    public async Task<ActionResult<IReadOnlyList<HomeAssistantDevice>>> GetAvailableDevices(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _homeAssistant.GetMobileAppDevicesAsync(cancellationToken));
        }
        catch (HomeAssistantException e)
        {
            return BadRequest(e.Message);
        }
    }

    /// <summary>
    /// Schickt eine Testnachricht an alle gespeicherten Geräte des Nutzers. Ein Tipp darauf öffnet Zaster.
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult<NotificationTestResult>> SendTest(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            return Unauthorized();
        }

        if (!_homeAssistant.IsConfigured)
        {
            return BadRequest("Home Assistant ist nicht eingerichtet. Bitte HomeAssistant__Url und HomeAssistant__Token in der Konfiguration setzen.");
        }

        if (!HomeAssistantClient.TryParseHttpUri(_options.ZasterUrl, out var zasterUrl))
        {
            return BadRequest("Die Adresse von Zaster fehlt. Bitte HomeAssistant__ZasterUrl in der Konfiguration setzen (so, wie das Handy Zaster erreicht).");
        }

        if (user.NotificationDevices.Count == 0)
        {
            return BadRequest("Bitte zuerst mindestens ein Gerät auswählen und speichern.");
        }

        var results = new List<NotificationDeviceResult>();
        foreach (var device in user.NotificationDevices)
        {
            try
            {
                await _homeAssistant.SendNotificationAsync(
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

        return Ok(new NotificationTestResult(results));
    }

    private NotificationSettingsDto ToDto(IReadOnlyList<string> devices) => new(
        _homeAssistant.IsConfigured,
        HomeAssistantClient.TryParseHttpUri(_options.ZasterUrl, out _),
        devices);
}
