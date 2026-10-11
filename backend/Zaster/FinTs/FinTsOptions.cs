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
    /// Zeitzone für die Abrufzeiten, die an den Konten eingestellt sind.
    /// </summary>
    public string TimeZone { get; set; } = "Europe/Berlin";
}
