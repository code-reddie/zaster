using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Zaster.Categories;
using Zaster.Database;

namespace Zaster.Transactions;

/// <summary>
/// Legt Buchungen in einem Konto an, überspringt solche, die dort schon liegen,
/// und wendet die Regeln des Kontos auf die neuen an. Gemeinsamer Weg für CSV und FinTS.
/// </summary>
public sealed class TransactionImporter(AppDbContext context, TimeProvider timeProvider)
{
    public async Task<ImportResult> ImportAsync(
        int accountId,
        IReadOnlyList<ImportTransaction> incoming,
        TransactionSource source,
        CancellationToken cancellationToken)
    {
        // Ohne Kategorien laden: Nur der Kontostand wird bei vorhandenen Buchungen nachgetragen.
        var existing = await context.Transactions
            .Where(t => t.AccountId == accountId)
            .ToListAsync(cancellationToken);

        var matches = TransactionMatcher.Match(
            existing,
            t => TransactionMatcher.Key(t.Buchung, t.Betrag, t.Auftraggeber),
            incoming,
            t => TransactionMatcher.Key(t.Buchung, t.Betrag, t.Auftraggeber));

        // Buchungen aus älteren Abrufen kennen ihren Kontostand noch nicht; ein neuer Abruf oder CSV-Import ergänzt ihn.
        foreach (var (item, match) in matches)
        {
            if (match is { Saldo: null } && item.Saldo is { } saldo)
            {
                match.Saldo = saldo;
            }
        }

        var newItems = matches.Where(m => m.Existing is null).Select(m => m.Item).ToList();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var transactions = newItems
            .Select(t => new Transaction
            {
                AccountId = accountId,
                Buchung = t.Buchung,
                Valuta = t.Valuta ?? t.Buchung,
                Auftraggeber = t.Auftraggeber.Trim(),
                Buchungstext = t.Buchungstext.Trim(),
                Verwendungszweck = t.Verwendungszweck?.Trim() ?? string.Empty,
                Betrag = t.Betrag,
                Saldo = t.Saldo,
                Source = source,
                CreatedAt = now,
            })
            .ToList();

        var rules = await context.Rules.Where(r => r.AccountId == accountId).ToListAsync(cancellationToken);
        RuleEngine.Apply(transactions, rules);

        context.Transactions.AddRange(transactions);
        await context.SaveChangesAsync(cancellationToken);

        return new ImportResult(transactions.Count, incoming.Count - transactions.Count);
    }
}
