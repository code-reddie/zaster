using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zaster.FinTs;

namespace Zaster.Backup;

/// <summary>
/// Sichert die Datenbank jeden Tag zur eingestellten Uhrzeit in den Zielordner. Prüft jede Minute,
/// damit eine geänderte Uhrzeit sofort gilt. Ohne Zielordner läuft der Dienst gar nicht erst los.
/// </summary>
public sealed partial class ScheduledBackupService(
    IServiceScopeFactory scopeFactory,
    IOptions<BackupOptions> backupOptions,
    IOptions<FinTsOptions> finTsOptions,
    TimeProvider timeProvider,
    ILogger<ScheduledBackupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(backupOptions.Value.Directory))
        {
            return;
        }

        var timeZone = FindTimeZone(finTsOptions.Value.TimeZone);
        var lastCheck = timeProvider.GetUtcNow();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = timeProvider.GetUtcNow();
                var nextMinute = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero).AddMinutes(1);
                await Task.Delay(nextMinute - now + TimeSpan.FromSeconds(1), timeProvider, stoppingToken);

                now = timeProvider.GetUtcNow();
                await RunIfDueAsync(lastCheck, now, timeZone, stoppingToken);
                lastCheck = now;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogCheckFailed(logger, ex);
            }
        }
    }

    private async Task RunIfDueAsync(DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var backup = scope.ServiceProvider.GetRequiredService<NasBackup>();
        var settings = await backup.GetSettingsAsync(cancellationToken);
        if (settings.Time is not { } time || !ScheduledSyncService.IsDue([time], from, to, timeZone))
        {
            return;
        }

        if (await backup.RunAsync(cancellationToken))
        {
            LogBackedUp(logger, backup.Directory!);
        }
        else
        {
            LogBackupFailed(logger, settings.LastError ?? "unbekannter Fehler");
        }
    }

    private TimeZoneInfo FindTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Prüfung auf fällige Sicherung fehlgeschlagen.")]
    static partial void LogCheckFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Datenbank nach {Directory} gesichert.")]
    static partial void LogBackedUp(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Automatische Sicherung fehlgeschlagen: {Error}")]
    static partial void LogBackupFailed(ILogger logger, string error);
}
