using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zaster.Database;

namespace Zaster.FinTs;

/// <summary>
/// Ruft die Umsätze der Konten mit gespeicherter PIN zu den Uhrzeiten ab, die am Konto eingestellt sind.
/// Prüft jede Minute, damit geänderte Uhrzeiten sofort gelten.
/// </summary>
public sealed partial class ScheduledSyncService(
    IServiceScopeFactory scopeFactory,
    IOptions<FinTsOptions> options,
    TimeProvider timeProvider,
    ILogger<ScheduledSyncService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly FinTsOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<ScheduledSyncService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timeZone = FindTimeZone(_options.TimeZone);
        var lastCheck = _timeProvider.GetUtcNow();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Bis kurz nach der nächsten vollen Minute warten.
                var now = _timeProvider.GetUtcNow();
                var nextMinute = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero).AddMinutes(1);
                await Task.Delay(nextMinute - now + TimeSpan.FromSeconds(1), _timeProvider, stoppingToken);

                now = _timeProvider.GetUtcNow();
                await SyncDueAccountsAsync(lastCheck, now, timeZone, stoppingToken);
                lastCheck = now;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogCheckFailed(_logger, ex);
            }
        }
    }

    /// <summary>
    /// Ob eine der Uhrzeiten <paramref name="times"/> (HH:mm) in der Zeitzone in den Zeitraum
    /// nach <paramref name="from"/> bis einschließlich <paramref name="to"/> fällt.
    /// </summary>
    internal static bool IsDue(IEnumerable<string> times, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone) =>
        times
            .Select(SyncTimes.Parse)
            .OfType<TimeOnly>()
            .Any(time => NextRun(from, time, timeZone) <= to);

    /// <summary>
    /// Nächster Zeitpunkt nach <paramref name="now"/>, an dem es in der Zeitzone <paramref name="time"/> Uhr ist.
    /// </summary>
    internal static DateTimeOffset NextRun(DateTimeOffset now, TimeOnly time, TimeZoneInfo timeZone)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);

        for (var day = 0; day < 3; day++)
        {
            var candidate = localDate.AddDays(day).ToDateTime(time, DateTimeKind.Unspecified);
            if (timeZone.IsInvalidTime(candidate))
            {
                // Zeitumstellung: die Uhrzeit gibt es an diesem Tag nicht.
                candidate = candidate.AddHours(1);
            }

            var utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(candidate, timeZone), TimeSpan.Zero);
            if (utc > now)
            {
                return utc;
            }
        }

        return now.AddDays(1);
    }

    private async Task SyncDueAccountsAsync(DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var syncService = scope.ServiceProvider.GetRequiredService<AccountSyncService>();

        var accounts = await context.Accounts
            .Where(a => a.FinTsPin != null)
            .ToListAsync(cancellationToken);

        foreach (var account in accounts.Where(a => IsDue(a.SyncTimes, from, to, timeZone)))
        {
            try
            {
                var result = await syncService.SyncAsync(account, null, null, false, cancellationToken);
                if (result is { Success: true })
                {
                    LogSynced(_logger, account.Id, result.Imported, result.Skipped);
                }
                else
                {
                    LogSyncFailed(_logger, account.Id, result?.Error ?? account.LastSyncError ?? "Keine PIN");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogSyncCrashed(_logger, account.Id, ex);
            }
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
            LogUnknownTimeZone(_logger, id);
            return TimeZoneInfo.Utc;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Zeitzone '{TimeZone}' unbekannt, der automatische Abruf nutzt UTC.")]
    static partial void LogUnknownTimeZone(ILogger logger, string timeZone);

    [LoggerMessage(Level = LogLevel.Error, Message = "Prüfung auf fällige FinTS-Abrufe fehlgeschlagen.")]
    static partial void LogCheckFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Konto {AccountId}: {Imported} neue Buchungen, {Skipped} schon vorhanden.")]
    static partial void LogSynced(ILogger logger, int accountId, int imported, int skipped);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Konto {AccountId}: Abruf fehlgeschlagen: {Error}")]
    static partial void LogSyncFailed(ILogger logger, int accountId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Konto {AccountId}: Abruf abgestürzt.")]
    static partial void LogSyncCrashed(ILogger logger, int accountId, Exception exception);
}
