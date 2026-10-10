using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Zaster.Notifications;

/// <summary>
/// Spricht mit der REST-API von Home Assistant, um Benachrichtigungen über die Companion-App zu verschicken.
/// </summary>
public sealed partial class HomeAssistantClient(HttpClient httpClient, IOptions<HomeAssistantOptions> options)
{
    private const string MobileAppPrefix = "mobile_app_";

    private readonly HttpClient _httpClient = httpClient;
    private readonly HomeAssistantOptions _options = options.Value;

    public bool IsConfigured =>
        TryGetBaseUri(out _) && !string.IsNullOrWhiteSpace(_options.Token);

    /// <summary>
    /// Gültiger Name eines notify-Dienstes, ohne „notify.“ davor.
    /// </summary>
    public static bool IsValidServiceName(string service) => ServiceNameRegex().IsMatch(service);

    /// <summary>
    /// Liefert alle Dienste <c>notify.mobile_app_*</c>, also die Geräte mit der Home-Assistant-App.
    /// </summary>
    public async Task<IReadOnlyList<HomeAssistantDevice>> GetMobileAppDevicesAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/services", null, cancellationToken);
        await EnsureSuccessAsync(response, null, cancellationToken);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var notify = document.RootElement.EnumerateArray()
            .FirstOrDefault(d => d.TryGetProperty("domain", out var domain) && domain.GetString() == "notify");
        if (notify.ValueKind != JsonValueKind.Object || !notify.TryGetProperty("services", out var services))
        {
            return [];
        }

        return services.EnumerateObject()
            .Where(s => s.Name.StartsWith(MobileAppPrefix, StringComparison.Ordinal))
            .Select(s => new HomeAssistantDevice(s.Name, GetDisplayName(s)))
            .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Ruft <c>notify.&lt;service&gt;</c> auf. Ein Tipp auf die Nachricht öffnet <paramref name="url"/>.
    /// </summary>
    public async Task SendNotificationAsync(
        string service,
        string title,
        string message,
        Uri url,
        CancellationToken cancellationToken)
    {
        if (!IsValidServiceName(service))
        {
            throw new HomeAssistantException($"„{service}“ ist kein gültiger Dienstname.");
        }

        // iOS liest „url“, Android „clickAction“.
        var payload = new
        {
            title,
            message,
            data = new
            {
                url = url.AbsoluteUri,
                clickAction = url.AbsoluteUri,
            },
        };

        using var response = await SendAsync(
            HttpMethod.Post,
            $"api/services/notify/{service}",
            // Als String mit Content-Length statt gestreamt, das vertragen auch Reverse-Proxys vor Home Assistant.
            new StringContent(JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
            cancellationToken);
        await EnsureSuccessAsync(response, service, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (!TryGetBaseUri(out var baseUri) || string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new HomeAssistantException(
                "Home Assistant ist nicht eingerichtet. Bitte HomeAssistant__Url und HomeAssistant__Token in der Konfiguration setzen.");
        }

        using var request = new HttpRequestMessage(method, new Uri(baseUri, path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);

        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HomeAssistantException($"Home Assistant unter {baseUri} antwortet nicht.", e);
        }
        catch (HttpRequestException e)
        {
            throw new HomeAssistantException($"Home Assistant unter {baseUri} ist nicht erreichbar ({e.Message}).", e);
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string? service,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "Home Assistant lehnt den Token ab. Bitte HomeAssistant__Token prüfen (Long-Lived Access Token).",
            HttpStatusCode.BadRequest or HttpStatusCode.NotFound when service is not null =>
                $"Home Assistant kennt den Dienst notify.{service} nicht. Ist die Home-Assistant-App auf dem Gerät angemeldet?",
            _ => $"Home Assistant meldet Fehler {(int)response.StatusCode}: {await ReadShortBodyAsync(response, cancellationToken)}",
        };
        throw new HomeAssistantException(message);
    }

    private static async Task<string> ReadShortBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        return body.Length > 200 ? body[..200] + "…" : body;
    }

    private bool TryGetBaseUri(out Uri baseUri) => TryParseHttpUri(_options.Url, out baseUri);

    internal static bool TryParseHttpUri(string? value, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (!text.EndsWith('/'))
        {
            text += "/";
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static string GetDisplayName(JsonProperty service)
    {
        // Die Beschreibung lautet z. B. „Sends a notification message using the mobile_app_pixel_8 integration.“
        // und enthält keinen schöneren Namen, deshalb wird er aus dem Dienstnamen gebildet.
        var name = service.Name[MobileAppPrefix.Length..].Replace('_', ' ');
        return name.Length == 0 ? service.Name : char.ToUpperInvariant(name[0]) + name[1..];
    }

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex ServiceNameRegex();
}
