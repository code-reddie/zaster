using System;
using System.Collections.Generic;
using System.Linq;
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

    public async Task<FinTsTestResult> FetchTransactionsAsync(FinTsTestRequest request, CancellationToken cancellationToken)
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

        var connectionDetails = new ConnectionDetails
        {
            Url = _options.Url,
            FinTSVersion = FinTsVersion.v300,
            Blz = _options.Blz,
            Bic = _options.Bic,
            Iban = iban,
            Account = accountNumber,
            UserId = userId,
            Pin = request.Pin,
        };

        // libfintx protokolliert sonst Rohnachrichten (inklusive PIN) in eine Datei.
        var client = new FinTsClient(connectionDetails, bpdDataStore: new BdpInMemoryStore(), loggerFactory: NullLoggerFactory.Instance);

        var tanDialog = new TANDialog(
            dialog =>
            {
                // App-Freigabe: libfintx fragt den Status selbst ab, hier ist nichts einzugeben.
                // Eine echte TAN-Eingabe unterstützt der Verbindungstest noch nicht.
                return Task.FromResult<string>(dialog.IsDecoupled ? string.Empty : null!);
            },
            _ => Task.CompletedTask);

        var days = Math.Clamp(request.Days ?? MaxDays, 1, MaxDays);
        var startDate = DateTime.Today.AddDays(-days);

        HBCIDialogResult<List<SwiftStatement>> result;
        try
        {
            result = await client
                .Transactions(tanDialog, startDate, DateTime.Today)
                .WaitAsync(TimeSpan.FromSeconds(_options.MaxWaitForApprovalSeconds), cancellationToken);
        }
        catch (TimeoutException)
        {
            tanDialog.IsCancelWaitForApproval = true;
            return Failed("Die Bank hat nicht rechtzeitig geantwortet. Wurde eine Freigabe in der ING-App erwartet?");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFetchFailed(_logger, ex);
            return Failed($"Abruf fehlgeschlagen: {ex.Message}");
        }

        var messages = result.Messages
            .Select(m => new FinTsBankMessage(m.Code, m.Message))
            .ToList();

        if (!result.IsSuccess || result.Data is null)
        {
            var error = result.IsSCARequired
                ? "Die Bank verlangt eine Freigabe. Bitte einmal im ING-Banking einloggen und erneut versuchen."
                : "Die Bank hat den Abruf abgelehnt. Details stehen in den Bankmeldungen.";
            return new FinTsTestResult(false, error, messages, []);
        }

        var transactions = result.Data
            .SelectMany(statement => statement.SwiftTransactions)
            .Select(ToPreview)
            .OrderByDescending(t => t.Buchung)
            .ToList();

        return new FinTsTestResult(true, null, messages, transactions);
    }

    private static FinTsTransactionPreview ToPreview(SwiftTransaction transaction)
    {
        var verwendungszweck = string.IsNullOrWhiteSpace(transaction.SVWZ)
            ? transaction.Description
            : transaction.SVWZ;

        return new FinTsTransactionPreview(
            ToDate(transaction.EntryDate ?? transaction.ValueDate),
            ToDate(transaction.ValueDate),
            transaction.PartnerName?.Trim() ?? string.Empty,
            transaction.Text?.Trim() ?? string.Empty,
            verwendungszweck?.Trim() ?? string.Empty,
            transaction.Amount);
    }

    private static DateTimeOffset ToDate(DateTime date) =>
        new(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc));

    private static FinTsTestResult Failed(string error) => new(false, error, [], []);

    [LoggerMessage(Level = LogLevel.Error, Message = "FinTS-Abruf fehlgeschlagen.")]
    static partial void LogFetchFailed(ILogger logger, Exception exception);
}
