using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zaster.Database;

namespace Zaster.Notifications;

/// <summary>
/// Nimmt Meldungen über neue Buchungen ohne Kategorie entgegen. Verschickt werden sie im Hintergrund
/// (<see cref="OpenTransactionsNotifier"/>), damit ein langsames oder nicht erreichbares Home Assistant
/// weder den CSV-Import noch den FinTS-Abruf aufhält.
/// </summary>
public sealed class OpenTransactionsQueue
{
    private readonly Channel<OpenTransactionsNotification> _channel =
        Channel.CreateUnbounded<OpenTransactionsNotification>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(int accountId, int newOpen) =>
        _channel.Writer.TryWrite(new OpenTransactionsNotification(accountId, newOpen));

    internal ChannelReader<OpenTransactionsNotification> Reader => _channel.Reader;
}

public sealed record OpenTransactionsNotification(int AccountId, int NewOpen);

/// <summary>
/// Schickt allen Mitgliedern eines Kontos eine Nachricht an ihre Geräte, wenn ein Abruf oder Import
/// Buchungen gebracht hat, die nach den Regeln noch ohne Kategorie sind. Ein Tipp öffnet „Ohne Kategorie“.
/// </summary>
public sealed partial class OpenTransactionsNotifier(
    OpenTransactionsQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<HomeAssistantOptions> options,
    ILogger<OpenTransactionsNotifier> logger) : BackgroundService
{
    private readonly HomeAssistantOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var notification in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await SendAsync(notification, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogFailed(logger, notification.AccountId, ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task SendAsync(OpenTransactionsNotification notification, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var homeAssistant = scope.ServiceProvider.GetRequiredService<HomeAssistantClient>();
        if (!homeAssistant.IsConfigured)
        {
            return;
        }

        if (!HomeAssistantClient.TryParseHttpUri(_options.ZasterUrl, out var zasterUrl))
        {
            LogNoZasterUrl(logger, notification.AccountId);
            return;
        }

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await context.Accounts
            .Where(a => a.Id == notification.AccountId)
            .Select(a => new
            {
                a.Name,
                Devices = a.Users.Select(u => u.NotificationDevices).ToList(),
                Open = a.Transactions.Count(t => !t.Splits.Any() && !t.Reviewed),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return;
        }

        // Teilen sich zwei Nutzer ein Gerät, kommt die Nachricht trotzdem nur einmal an.
        var devices = account.Devices.SelectMany(d => d).Distinct().ToList();
        var message = Message(notification.NewOpen, account.Open);
        var url = Link(zasterUrl, notification.AccountId);

        foreach (var device in devices)
        {
            try
            {
                await homeAssistant.SendNotificationAsync(device, $"Zaster · {account.Name}", message, url, cancellationToken);
            }
            catch (HomeAssistantException e)
            {
                LogDeviceFailed(logger, notification.AccountId, device, e.Message);
            }
        }
    }

    /// <summary>
    /// Text der Nachricht, z. B. „3 neue Buchungen ohne Kategorie (insgesamt 12 offen)“.
    /// </summary>
    internal static string Message(int newOpen, int totalOpen)
    {
        var text = newOpen == 1 ? "1 neue Buchung ohne Kategorie" : $"{newOpen} neue Buchungen ohne Kategorie";
        return totalOpen > newOpen ? $"{text} (insgesamt {totalOpen} offen)" : text;
    }

    /// <summary>
    /// Seite „Ohne Kategorie“ des Kontos. <paramref name="zasterUrl"/> endet immer auf „/“, ein Pfad darin bleibt erhalten.
    /// </summary>
    internal static Uri Link(Uri zasterUrl, int accountId) => new(zasterUrl, $"konten/{accountId}/offen");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Konto {AccountId}: Keine Benachrichtigung, HomeAssistant__ZasterUrl fehlt.")]
    static partial void LogNoZasterUrl(ILogger logger, int accountId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Konto {AccountId}: Benachrichtigung an {Device} fehlgeschlagen: {Error}")]
    static partial void LogDeviceFailed(ILogger logger, int accountId, string device, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Konto {AccountId}: Benachrichtigung fehlgeschlagen.")]
    static partial void LogFailed(ILogger logger, int accountId, Exception exception);
}
