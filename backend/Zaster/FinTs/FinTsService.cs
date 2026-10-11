using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using libfintx.FinTS;
using libfintx.FinTS.BankParameterData;
using libfintx.FinTS.Data;
using libfintx.Swift;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Zaster.FinTs;

public sealed partial class FinTsService(IOptions<FinTsOptions> options, ILogger<FinTsService> logger)
{
    private const int MaxDays = 90;

    private readonly FinTsOptions _options = options.Value;
    private readonly ILogger<FinTsService> _logger = logger;

    public async Task<FinTsFetchResult> FetchTransactionsAsync(FinTsFetchRequest request, CancellationToken cancellationToken)
    {
        var iban = request.Iban.Replace(" ", string.Empty).ToUpperInvariant();
        if (iban.Length != 22 || !iban.StartsWith("DE", StringComparison.Ordinal))
        {
            return Failed("Bitte eine deutsche IBAN mit 22 Zeichen angeben.");
        }

        var blz = iban.Substring(4, 8);
        if (blz != _options.Blz.ToString())
        {
            return Failed($"Die IBAN gehört nicht zur Bankleitzahl {_options.Blz}. Bisher wird nur die ING unterstützt.");
        }

        var accountNumber = iban.Substring(12, 10);
        var userId = string.IsNullOrWhiteSpace(request.UserId) ? accountNumber : request.UserId.Trim();

        if (!IsAscii(request.Pin) || !IsAscii(userId))
        {
            return Failed("PIN oder Zugangsnummer enthalten Umlaute oder andere Zeichen außerhalb von ASCII. Diese kann libfintx nicht an die Bank übertragen.");
        }

        var connectionDetails = new ConnectionDetails
        {
            Url = _options.Url,
            FinTSVersion = FinTsVersion.v300,
            Blz = _options.Blz,
            Bic = _options.Bic,
            Iban = iban,
            Account = accountNumber,
            // libfintx 1.4.0 maskiert ?, + und andere FinTS-Sonderzeichen nicht vollständig.
            // Unmaskierte Sonderzeichen machen die Nachricht unlesbar (ING-Meldung 9030).
            UserId = FinTsMessages.EscapeUserId(userId),
            Pin = FinTsMessages.Escape(request.Pin),
        };

        // libfintx protokolliert sonst Rohnachrichten (inklusive PIN) in eine Datei.
        var client = new FinTsClient(connectionDetails, bpdDataStore: new BdpInMemoryStore(), loggerFactory: NullLoggerFactory.Instance);

        var tanDialog = new TANDialog(
            dialog =>
            {
                // App-Freigabe: libfintx fragt den Status selbst ab, hier ist nichts einzugeben.
                // Eine echte TAN-Eingabe unterstützt Zaster noch nicht.
                return Task.FromResult<string>(dialog.IsDecoupled ? string.Empty : null!);
            },
            _ => Task.CompletedTask);

        var days = Math.Clamp(request.Days ?? MaxDays, 1, MaxDays);
        var startDate = DateTime.Today.AddDays(-days);

        var messages = new List<FinTsBankMessage>();
        var diagnostics = new List<string>();

        HBCIDialogResult<List<SwiftStatement>> result;
        try
        {
            var sync = await client
                .Synchronization()
                .WaitAsync(TimeSpan.FromSeconds(_options.MaxWaitForApprovalSeconds), cancellationToken);
            AddMessages(messages, sync);
            diagnostics.Add($"Synchronisation: {DescribeSegments(sync.RawData)}");
            diagnostics.AddRange(FinTsMessages.ResultSegments(sync.RawData));

            if (sync.HasError || FinTsMessages.IsRejected(messages))
            {
                return new FinTsFetchResult(false, "Die ING hat die Anmeldung abgelehnt. Details stehen in den Bankmeldungen.", messages, [], diagnostics, LoginRejected: true);
            }

            // libfintx erkennt die Bankparameterdaten nur, wenn sie in einer Zeile stehen.
            // Ohne sie stürzt der Umsatzabruf mit einer NullReferenceException ab.
            client.BPD ??= ParseBpd(sync.RawData);
            if (client.BPD is null)
            {
                var error = messages.Count > 0
                    ? "Die ING hat keine Bankparameterdaten geschickt. Bitte die Meldungen und die Diagnose unten an Claude schicken."
                    : "Die ING hat auf die Anmeldung ohne verwertbare Antwort reagiert. Bitte die Diagnose unten an Claude schicken.";
                return new FinTsFetchResult(false, error, messages, [], diagnostics);
            }

            if (!string.IsNullOrEmpty(client.SystemId))
            {
                connectionDetails.CustomerSystemId = client.SystemId;
            }

            result = await client
                .Transactions(tanDialog, startDate, DateTime.Today)
                .WaitAsync(TimeSpan.FromSeconds(_options.MaxWaitForApprovalSeconds), cancellationToken);
            AddMessages(messages, result);
            diagnostics.Add($"Umsatzabruf: {DescribeSegments(result.RawData)}");
            diagnostics.AddRange(FinTsMessages.ResultSegments(result.RawData));
        }
        catch (TimeoutException)
        {
            tanDialog.IsCancelWaitForApproval = true;
            return new FinTsFetchResult(false, "Die Bank hat nicht rechtzeitig geantwortet. Wurde eine Freigabe in der ING-App erwartet?", messages, [], diagnostics);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFetchFailed(_logger, ex);
            diagnostics.Add($"Fehler: {ex.GetType().Name} in {ex.TargetSite?.DeclaringType?.Name}.{ex.TargetSite?.Name}");
            return new FinTsFetchResult(false, $"Abruf fehlgeschlagen: {ex.Message}", messages, [], diagnostics);
        }

        // Warnungen wie 3010 („nur die letzten 90 Tage“) kommen ohne Erfolgsmeldung, die Umsätze sind trotzdem da.
        if (result.HasError || FinTsMessages.IsRejected(messages) || result.Data is null)
        {
            var error = result.IsSCARequired
                ? "Die Bank verlangt eine Freigabe. Bitte einmal im ING-Banking einloggen und erneut versuchen."
                : "Die Bank hat den Abruf abgelehnt. Details stehen in den Bankmeldungen.";
            return new FinTsFetchResult(false, error, messages, [], diagnostics);
        }

        diagnostics.Add($"Kontoauszüge: {result.Data.Count}, Buchungen: {result.Data.Sum(statement => statement.SwiftTransactions.Count)}");

        // Vorgemerkte Umsätze (MT942) tragen bei der ING ein vorläufiges Datum in der Zukunft und kämen
        // nach der echten Buchung ein zweites Mal herein. Erst gebuchte Umsätze übernehmen.
        var transactions = result.Data
            .Where(statement => !statement.Pending)
            .SelectMany(statement => statement.SwiftTransactions)
            .Select(ToTransaction)
            .OrderByDescending(t => t.Buchung)
            .ToList();

        return new FinTsFetchResult(true, null, messages, transactions, diagnostics);
    }

    private static FinTsTransaction ToTransaction(SwiftTransaction transaction)
    {
        var verwendungszweck = string.IsNullOrWhiteSpace(transaction.SVWZ)
            ? transaction.Description
            : transaction.SVWZ;

        return new FinTsTransaction(
            ToDate(transaction.EntryDate ?? transaction.ValueDate),
            ToDate(transaction.ValueDate),
            transaction.PartnerName?.Trim() ?? string.Empty,
            transaction.Text?.Trim() ?? string.Empty,
            verwendungszweck?.Trim() ?? string.Empty,
            transaction.Amount);
    }

    private static DateOnly ToDate(DateTime date) => DateOnly.FromDateTime(date);

    private static bool IsAscii(string value) => value.All(char.IsAscii);

    private static FinTsFetchResult Failed(string error) => new(false, error, [], [], []);

    private static void AddMessages(List<FinTsBankMessage> messages, HBCIDialogResult result)
    {
        var parsed = result.Messages.Select(m => new FinTsBankMessage(m.Code, m.Message)).ToList();

        // libfintx übersieht Rückmeldungen, die nicht genau seinem Muster entsprechen.
        if (parsed.Count == 0)
        {
            parsed = FinTsMessages.ParseResultMessages(result.RawData);
        }

        messages.AddRange(parsed.Where(m => !messages.Contains(m)));
    }

    private static BPD? ParseBpd(string? rawData)
    {
        if (string.IsNullOrEmpty(rawData))
        {
            return null;
        }

        var match = BpdRegex().Match(rawData);
        return match.Success ? BPD.Parse(match.Groups[1].Value, NullLogger.Instance) : null;
    }

    /// <summary>
    /// Listet nur die Segmentkennungen einer Bankantwort auf, keine Inhalte.
    /// </summary>
    private static string DescribeSegments(string? rawData)
    {
        if (string.IsNullOrEmpty(rawData))
        {
            return "leere Antwort";
        }

        var segments = SegmentHeaderRegex().Matches(rawData).Select(m => m.Groups[1].Value).ToList();
        return segments.Count == 0
            ? $"keine Segmente erkannt ({rawData.Length} Zeichen)"
            : string.Join(", ", segments);
    }

    [GeneratedRegex(@"(HIBPA.+?)\b(?:HIUPA|HISYN|HNHBS)\b", RegexOptions.Singleline)]
    private static partial Regex BpdRegex();

    [GeneratedRegex(@"(?:^|')([A-Z]{5,6}:\d+:\d+)")]
    private static partial Regex SegmentHeaderRegex();

    [LoggerMessage(Level = LogLevel.Error, Message = "FinTS-Abruf fehlgeschlagen.")]
    static partial void LogFetchFailed(ILogger logger, Exception exception);
}
