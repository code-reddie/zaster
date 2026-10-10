namespace Zaster.Notifications;

public sealed class HomeAssistantOptions
{
    public const string SectionName = "HomeAssistant";

    /// <summary>
    /// Adresse von Home Assistant, wie Zaster sie erreicht, z. B. http://homeassistant.local:8123.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Long-Lived Access Token aus dem Home-Assistant-Profil. Geheim: wird nie an das Frontend gegeben oder geloggt.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Adresse von Zaster, wie das Handy sie erreicht. Ein Tipp auf die Benachrichtigung öffnet sie.
    /// </summary>
    public string? ZasterUrl { get; set; }
}
