using System;
using Zaster.Database;

namespace Zaster.Backup;

/// <summary>
/// Einstellungen und Stand der automatischen Sicherung. Es gibt genau eine Zeile (Id 1), sie gilt für ganz Zaster.
/// </summary>
public sealed record BackupSettings : Entity
{
    public const int SingletonId = 1;

    public const string DefaultTime = "03:00";

    /// <summary>
    /// Uhrzeit („HH:mm“, Zeitzone <c>FinTS:TimeZone</c>), zu der Zaster jeden Tag sichert. Null = keine automatische Sicherung.
    /// </summary>
    public string? Time { get; set; } = DefaultTime;

    /// <summary>
    /// Zeitpunkt der letzten erfolgreichen Sicherung (UTC).
    /// </summary>
    public DateTime? LastBackupAt { get; set; }

    /// <summary>
    /// Fehlermeldung der letzten Sicherung, null nach einer erfolgreichen.
    /// </summary>
    public string? LastError { get; set; }
}

/// <param name="Configured">Ob ein Zielordner eingestellt ist (<c>Backup__Directory</c>).</param>
/// <param name="Directory">Zielordner im Container.</param>
/// <param name="FileName">Name der Datei im Zielordner.</param>
public sealed record BackupScheduleDto(
    bool Configured,
    string? Directory,
    string FileName,
    string? Time,
    DateTime? LastBackupAt,
    string? LastError);

/// <param name="Time">„HH:mm“ oder leer für keine automatische Sicherung.</param>
public sealed record SaveBackupSchedule(string? Time);
