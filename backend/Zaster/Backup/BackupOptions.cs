namespace Zaster.Backup;

public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>
    /// Zielordner für die automatische Sicherung, z. B. die per docker compose eingebundene NAS-Freigabe der FritzBox.
    /// Leer = keine automatische Sicherung.
    /// </summary>
    public string? Directory { get; set; }
}
