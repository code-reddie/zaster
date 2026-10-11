using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zaster.Database;
using Zaster.FinTs;
using Zaster.Notifications;

namespace Zaster.Backup;

/// <summary>
/// Sichert die Datenbank jeden Tag zur eingestellten Uhrzeit in den Zielordner. Prüft jede Minute,
/// damit eine geänderte Uhrzeit sofort gilt. Ohne Zielordner läuft der Dienst gar nicht erst los.
/// </summary>
public sealed partial class ScheduledBackupService(
    IServiceScopeFactory scopeFactory,
    IOptions<BackupOptions> backupOptions,
    IOptions<FinTsOptions> finTsOptions,
    IOptions<HomeAssistantOptions> homeAssistantOptions,
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
            var error = settings.LastError ?? "unbekannter Fehler";
            LogBackupFailed(logger, error);
            await NotifyFailureAsync(scope.ServiceProvider, error, cancellationToken);
        }
    }

    /// <summary>
    /// Meldet eine fehlgeschlagene Sicherung an die Geräte dessen, der Zaster eingerichtet hat (nur er sieht die Sicherung).
    /// Ein Tipp öffnet die Einstellungen mit der Fehlermeldung. Fehler beim Senden landen nur im Log.
    /// </summary>
    private async Task NotifyFailureAsync(IServiceProvider services, string error, CancellationToken cancellationToken)
    {
        var homeAssistant = services.GetRequiredService<HomeAssistantClient>();
        if (!homeAssistant.IsConfigured)
        {
            return;
        }

        if (!HomeAssistantClient.TryParseHttpUri(homeAssistantOptions.Value.ZasterUrl, out var zasterUrl))
        {
            LogNoZasterUrl(logger);
            return;
        }

        var context = services.GetRequiredService<AppDbContext>();
        var devices = await context.Users
            .OrderBy(u => u.Id)
            .Select(u => u.NotificationDevices)
            .FirstOrDefaultAsync(cancellationToken) ?? [];

        foreach (var device in devices)
        {
            try
            {
                await homeAssistant.SendNotificationAsync(
                    device, "Zaster · Sicherung", FailureMessage(error), SettingsLink(zasterUrl), cancellationToken);
            }
            catch (HomeAssistantException e)
            {
                LogNotifyFailed(logger, device, e.Message);
            }
        }
    }

    internal static string FailureMessage(string error) => $"Die automatische Sicherung ist fehlgeschlagen: {error}";

    /// <summary>
    /// Seite „Einstellungen“. <paramref name="zasterUrl"/> endet immer auf „/“, ein Pfad darin bleibt erhalten.
    /// </summary>
    internal static Uri SettingsLink(Uri zasterUrl) => new(zasterUrl, "einstellungen");

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Keine Benachrichtigung über die fehlgeschlagene Sicherung, HomeAssistant__ZasterUrl fehlt.")]
    static partial void LogNoZasterUrl(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Benachrichtigung über die fehlgeschlagene Sicherung an {Device} fehlgeschlagen: {Error}")]
    static partial void LogNotifyFailed(ILogger logger, string device, string error);
}
