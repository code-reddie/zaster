using System;
using System.Globalization;
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
/// Ruft jede Nacht die Umsätze aller Konten mit gespeicherter PIN ab.
/// </summary>
public sealed partial class NightlySyncService(
    IServiceScopeFactory scopeFactory,
    IOptions<FinTsOptions> options,
    TimeProvider timeProvider,
    ILogger<NightlySyncService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly FinTsOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<NightlySyncService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.NightlySyncTime))
        {
            LogDisabled(_logger);
            return;
        }

        if (!TimeOnly.TryParseExact(_options.NightlySyncTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            LogInvalidTime(_logger, _options.NightlySyncTime);
            return;
        }

        var timeZone = FindTimeZone(_options.TimeZone);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _timeProvider.GetUtcNow();
            var next = NextRun(now, time, timeZone);
            LogNextRun(_logger, next);

            try
            {
                await Task.Delay(next - now, _timeProvider, stoppingToken);
                await SyncAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

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

    private async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var syncService = scope.ServiceProvider.GetRequiredService<AccountSyncService>();

        var accounts = await context.Accounts
            .Include(a => a.Users)
            .Where(a => a.FinTsPin != null)
            .ToListAsync(cancellationToken);

        foreach (var account in accounts)
        {
            // Kategorie-Regeln gehören Nutzern; bei geteilten Konten gelten die des ersten.
            var userId = account.Users.Select(u => u.Id).DefaultIfEmpty().Min();
            if (userId == 0)
            {
                continue;
            }

            try
            {
                var result = await syncService.SyncAsync(account, userId, null, null, false, cancellationToken);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Nächtlicher FinTS-Abruf ist abgeschaltet.")]
    static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "FinTS:NightlySyncTime '{Time}' ist keine Uhrzeit im Format HH:mm, der nächtliche Abruf ist abgeschaltet.")]
    static partial void LogInvalidTime(ILogger logger, string time);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Zeitzone '{TimeZone}' unbekannt, der nächtliche Abruf nutzt UTC.")]
    static partial void LogUnknownTimeZone(ILogger logger, string timeZone);

    [LoggerMessage(Level = LogLevel.Information, Message = "Nächster FinTS-Abruf um {Next:u}.")]
    static partial void LogNextRun(ILogger logger, DateTimeOffset next);

    [LoggerMessage(Level = LogLevel.Information, Message = "Konto {AccountId}: {Imported} neue Buchungen, {Skipped} schon vorhanden.")]
    static partial void LogSynced(ILogger logger, int accountId, int imported, int skipped);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Konto {AccountId}: Abruf fehlgeschlagen: {Error}")]
    static partial void LogSyncFailed(ILogger logger, int accountId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Konto {AccountId}: Abruf abgestürzt.")]
    static partial void LogSyncCrashed(ILogger logger, int accountId, Exception exception);
}
