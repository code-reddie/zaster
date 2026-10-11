using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Zaster.Accounts;
using Zaster.Database;
using Zaster.Transactions;

namespace Zaster.FinTs;

/// <summary>
/// Ruft die Umsätze eines Kontos per FinTS ab und übernimmt die neuen.
/// Wird vom Abruf in der Oberfläche und vom automatischen Abruf genutzt.
/// </summary>
public sealed class AccountSyncService(
    AppDbContext context,
    FinTsService finTsService,
    FinTsPinProtector pinProtector,
    TransactionImporter importer,
    TimeProvider timeProvider)
{
    private const int MaxDays = 90;

    // Überlappung zum letzten Abruf, damit nachträglich gebuchte Umsätze nicht fehlen.
    private const int OverlapDays = 7;

    /// <summary>
    /// Ohne eingegebene PIN wird die gespeicherte verwendet.
    /// </summary>
    /// <returns>Das Ergebnis, oder null wenn keine PIN vorliegt.</returns>
    public async Task<FinTsSyncResult?> SyncAsync(
        Account account,
        string? enteredUserId,
        string? enteredPin,
        bool savePin,
        CancellationToken cancellationToken)
    {
        enteredPin = string.IsNullOrWhiteSpace(enteredPin) ? null : enteredPin;
        enteredUserId = string.IsNullOrWhiteSpace(enteredUserId) ? null : enteredUserId.Trim();

        var pin = enteredPin ?? (account.FinTsPin is null ? null : pinProtector.Unprotect(account.FinTsPin));
        if (pin is null)
        {
            if (account.FinTsPin is not null)
            {
                // Der Schlüssel zum Entschlüsseln fehlt, z. B. nach einem neuen Container ohne /data/keys.
                account.FinTsPin = null;
                account.LastSyncError = "Die gespeicherte PIN konnte nicht entschlüsselt werden und wurde gelöscht. Bitte gib sie neu ein.";
                await context.SaveChangesAsync(cancellationToken);
            }

            return null;
        }

        var now = timeProvider.GetUtcNow();
        var days = account.LastSyncedAt is { } lastSynced
            ? Math.Clamp((now - lastSynced).Days + OverlapDays, 1, MaxDays)
            : MaxDays;

        var fetched = await finTsService.FetchTransactionsAsync(
            new FinTsFetchRequest(account.Iban, enteredUserId ?? account.FinTsUserId, pin, days),
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
            await context.SaveChangesAsync(cancellationToken);
            return new FinTsSyncResult(false, error, fetched.Messages, fetched.Diagnostics, 0, 0);
        }

        if (enteredPin is not null && savePin)
        {
            account.FinTsPin = pinProtector.Protect(enteredPin);
        }

        if (enteredUserId is not null)
        {
            account.FinTsUserId = enteredUserId;
        }

        account.LastSyncedAt = now;
        account.LastSyncError = null;

        await RemovePendingAsync(account.Id, now, cancellationToken);

        var imported = await importer.ImportAsync(
            account.Id,
            fetched.Transactions
                .Select(t => new ImportTransaction(t.Buchung, t.Valuta, t.Auftraggeber, t.Buchungstext, t.Verwendungszweck, t.Betrag))
                .ToList(),
            TransactionSource.FinTs,
            cancellationToken);

        return new FinTsSyncResult(true, null, fetched.Messages, fetched.Diagnostics, imported.Imported, imported.Skipped);
    }

    /// <summary>
    /// Frühere Versionen haben auch vorgemerkte Umsätze übernommen. Die ING datiert sie in die Zukunft,
    /// gebuchte Umsätze liegen nie dort. Solche Reste werden entfernt, die echte Buchung kommt per Abruf.
    /// </summary>
    private Task<int> RemovePendingAsync(int accountId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Ein Tag Spielraum, damit Zeitzonen keine echte Buchung von heute treffen.
        var latest = DateOnly.FromDateTime(now.UtcDateTime).AddDays(1);
        return context.Transactions
            .Where(t => t.AccountId == accountId
                && (t.Source == TransactionSource.FinTs || t.Source == TransactionSource.Unbekannt)
                && t.Buchung > latest)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
