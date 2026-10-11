namespace Zaster.FinTs;

public sealed class FinTsOptions
{
    public const string SectionName = "FinTS";

    /// <summary>
    /// Produktregistrierungsnummer der Deutschen Kreditwirtschaft.
    /// Ohne eigene Nummer wird die von libfintx verwendet.
    /// </summary>
    public string? ProductId { get; set; }

    public string Url { get; set; } = "https://fints.ing.de/fints/";

    public int Blz { get; set; } = 50010517;

    public string Bic { get; set; } = "INGDDEFFXXX";

    public int MaxWaitForApprovalSeconds { get; set; } = 180;

    /// <summary>
    /// Ordner für die Schlüssel, mit denen gespeicherte PINs verschlüsselt werden.
    /// Ohne diese Schlüssel lassen sich die PINs nicht mehr entschlüsseln.
    /// Leer = Unterordner <c>keys</c> im Datenordner (<c>/data/keys</c>, lokal <c>./data/keys</c>).
    /// </summary>
    public string? KeyRingPath { get; set; }

    /// <summary>
    /// Uhrzeit (HH:mm), zu der Konten mit gespeicherter PIN jede Nacht abgerufen werden.
    /// Leer schaltet den nächtlichen Abruf ab.
    /// </summary>
    public string? NightlySyncTime { get; set; } = "04:00";

    /// <summary>
    /// Zeitzone für <see cref="NightlySyncTime"/>.
    /// </summary>
    public string TimeZone { get; set; } = "Europe/Berlin";
}
