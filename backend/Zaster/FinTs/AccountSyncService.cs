using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaster.Database;
using Zaster.Import;
using Zaster.Models;

namespace Zaster.FinTs;

/// <summary>
/// Ruft die Umsätze eines Kontos per FinTS ab und übernimmt die neuen.
/// Wird vom manuellen Abruf und vom nächtlichen Abruf genutzt.
/// </summary>
public sealed class AccountSyncService(
    AppDbContext context,
    FinTsService finTsService,
    FinTsPinProtector pinProtector,
    TransactionImporter importer)
{
    private const int MaxDays = 90;

    // Überlappung zum letzten Abruf, damit nachträglich gebuchte Umsätze nicht fehlen.
    private const int OverlapDays = 7;

    private readonly AppDbContext _context = context;
    private readonly FinTsService _finTsService = finTsService;
    private readonly FinTsPinProtector _pinProtector = pinProtector;
    private readonly TransactionImporter _importer = importer;

    /// <summary>
    /// Ohne eingegebene PIN wird die gespeicherte verwendet.
    /// </summary>
    /// <returns>Das Ergebnis, oder null wenn keine PIN vorliegt.</returns>
    public async Task<FinTsSyncResult?> SyncAsync(
        Account account,
        int userId,
        string? enteredUserId,
        string? enteredPin,
        bool savePin,
        CancellationToken cancellationToken)
    {
        enteredPin = string.IsNullOrWhiteSpace(enteredPin) ? null : enteredPin;
        enteredUserId = string.IsNullOrWhiteSpace(enteredUserId) ? null : enteredUserId.Trim();

        var pin = enteredPin ?? (account.FinTsPin is null ? null : _pinProtector.Unprotect(account.FinTsPin));
        if (pin is null)
        {
            if (account.FinTsPin is not null)
            {
                // Der Schlüssel zum Entschlüsseln fehlt, z. B. nach einem neuen Container ohne /data/keys.
                account.FinTsPin = null;
                account.LastSyncError = "Die gespeicherte PIN konnte nicht entschlüsselt werden und wurde gelöscht. Bitte gib sie neu ein.";
                await _context.SaveChangesAsync(cancellationToken);
            }

            return null;
        }

        var days = account.LastSyncedAt is { } lastSynced
            ? Math.Clamp((DateTimeOffset.UtcNow - lastSynced).Days + OverlapDays, 1, MaxDays)
            : MaxDays;

        var fetched = await _finTsService.FetchTransactionsAsync(
            new FinTsTestRequest(account.Iban, enteredUserId ?? account.FinTsUserId, pin, days),
            cancellationToken);

        if (!fetched.Success)
        {
            var error = fetched.Error;

            // Mehrere Fehlversuche mit einer falschen PIN sperren den Zugang bei der Bank.
            // Eine abgelehnte gespeicherte PIN wird daher nicht noch einmal verwendet.
            if (fetched.LoginRejected && enteredPin is null)
            {
                account.FinTsPin = null;
                error = "Die ING hat die Anmeldung mit der gespeicherten PIN abgelehnt. Die PIN wurde gelöscht, bitte gib sie neu ein.";
            }

            account.LastSyncError = error;
            await _context.SaveChangesAsync(cancellationToken);
            return new FinTsSyncResult(false, error, fetched.Messages, fetched.Diagnostics, 0, 0, [], AccountDto.From(account));
        }

        if (enteredPin is not null && savePin)
        {
            account.FinTsPin = _pinProtector.Protect(enteredPin);
        }

        if (enteredUserId is not null)
        {
            account.FinTsUserId = enteredUserId;
        }

        account.LastSyncedAt = DateTimeOffset.UtcNow;
        account.LastSyncError = null;

        var imported = await _importer.ImportAsync(
            account,
            userId,
            fetched.Transactions
                .Select(t => new ImportTransaction(t.Buchung, t.Valuta, t.Auftragsgeber, t.Buchungstext, t.Verwendungszweck, t.Betrag))
                .ToList(),
            cancellationToken);

        return new FinTsSyncResult(
            true,
            null,
            fetched.Messages,
            fetched.Diagnostics,
            imported.Imported,
            imported.Skipped,
            imported.Transactions,
            AccountDto.From(account));
    }
}
