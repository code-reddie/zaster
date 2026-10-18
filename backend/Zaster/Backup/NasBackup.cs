using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zaster.Database;
using Zaster.FinTs;

namespace Zaster.Backup;

/// <summary>
/// Automatische Sicherung in einen Zielordner (z. B. das NAS der FritzBox). Jede Sicherung bekommt einen Zeitstempel
/// im Namen, danach werden alte Sicherungen nach <see cref="BackupRetention"/> gelöscht.
/// </summary>
public sealed class NasBackup(
    AppDbContext context,
    IOptions<BackupOptions> options,
    IOptions<FinTsOptions> finTsOptions,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Zeitgesteuerte und von Hand gestartete Sicherung sollen nicht gleichzeitig in dieselbe Datei schreiben.
    /// </summary>
    private static readonly SemaphoreSlim Lock = new(1, 1);

    public string? Directory => string.IsNullOrWhiteSpace(options.Value.Directory) ? null : options.Value.Directory;

    public async Task<BackupSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await context.Set<BackupSettings>().FindAsync([BackupSettings.SingletonId], cancellationToken);
        if (settings is null)
        {
            settings = new BackupSettings { Id = BackupSettings.SingletonId };
            context.Add(settings);
            await context.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }

    public async Task<BackupScheduleDto> GetScheduleAsync(CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return new BackupScheduleDto(
            Directory is not null,
            Directory,
            BackupRetention.FileNamePattern,
            CountBackups(),
            settings.Time,
            // SQLite liefert DateTime ohne Art zurück; als UTC markieren, damit das Frontend die Zeit richtig umrechnet.
            settings.LastBackupAt is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null,
            settings.LastError);
    }

    /// <summary>
    /// Sichert die Datenbank in den Zielordner und merkt sich Erfolg oder Fehler. Ohne Zielordner passiert nichts.
    /// </summary>
    /// <returns>Ob die Sicherung geklappt hat.</returns>
    public async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        if (Directory is not { } directory)
        {
            return false;
        }

        await Lock.WaitAsync(cancellationToken);
        try
        {
            var settings = await GetSettingsAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            var fileName = BackupRetention.FileName(TimeZoneInfo.ConvertTime(now, FindTimeZone()).DateTime);
            settings.LastError = await TryWriteAsync(directory, fileName, cancellationToken);
            if (settings.LastError is null)
            {
                settings.LastBackupAt = now.UtcDateTime;
                DeleteObsolete(directory);
            }

            await context.SaveChangesAsync(cancellationToken);
            return settings.LastError is null;
        }
        finally
        {
            Lock.Release();
        }
    }

    /// <summary>
    /// Schreibt die Kopie erst lokal (SQLite auf einer Netzwerkfreigabe ist unzuverlässig), kopiert sie unter einem
    /// Zwischennamen auf das Ziel und benennt sie dann um. So liegt im Zielordner immer eine vollständige Sicherung,
    /// auch wenn die Verbindung mittendrin abreißt.
    /// </summary>
    /// <returns>Null bei Erfolg, sonst die Fehlermeldung.</returns>
    private async Task<string?> TryWriteAsync(string directory, string fileName, CancellationToken cancellationToken)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            return $"Den Zielordner {directory} gibt es nicht. Ist die Freigabe eingebunden?";
        }

        var local = Path.Combine(Path.GetTempPath(), $"zaster-sicherung-{Guid.NewGuid():N}.db");
        var target = Path.Combine(directory, fileName);
        var partial = target + ".tmp";
        try
        {
            await DatabaseBackup.WriteAsync(context, local, cancellationToken);

            await using (var source = File.OpenRead(local))
            await using (var destination = new FileStream(partial, FileMode.Create, FileAccess.Write))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            File.Move(partial, target, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(partial);
            return ex is UnauthorizedAccessException
                ? $"Keine Schreibrechte im Zielordner {directory}."
                : $"Die Sicherung konnte nicht nach {directory} geschrieben werden: {ex.Message}";
        }
        finally
        {
            TryDelete(local);
        }
    }

    /// <summary>
    /// Löscht die Sicherungen, die nicht mehr ins Raster passen. Nur nach einer erfolgreichen Sicherung, damit bei
    /// Ausfällen die alten erhalten bleiben. Klappt das Löschen nicht, wird es beim nächsten Mal wieder versucht.
    /// </summary>
    private static void DeleteObsolete(string directory)
    {
        try
        {
            foreach (var (path, _) in BackupRetention.Obsolete(BackupRetention.Find(directory), b => b.Time))
            {
                TryDelete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ordner nicht lesbar: beim nächsten Mal wieder.
        }
    }

    private int? CountBackups()
    {
        if (Directory is not { } directory || !System.IO.Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            return BackupRetention.Find(directory).Count;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private TimeZoneInfo FindTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(finTsOptions.Value.TimeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Bleibt liegen und wird beim nächsten Mal wieder versucht.
        }
    }
}
