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
        var existing = await context.Transactions
            .Where(t => t.AccountId == accountId)
            .Select(t => new { t.Buchung, t.Betrag, t.Auftraggeber })
            .ToListAsync(cancellationToken);

        var newItems = TransactionMatcher.SelectNew(
            existing.Select(t => TransactionMatcher.Key(t.Buchung, t.Betrag, t.Auftraggeber)),
            incoming,
            t => TransactionMatcher.Key(t.Buchung, t.Betrag, t.Auftraggeber));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var transactions = newItems
            .Select(t => new Transaction
            {
                AccountId = accountId,
                Buchung = t.Buchung,
                Valuta = t.Valuta,
                Auftraggeber = t.Auftraggeber.Trim(),
                Buchungstext = t.Buchungstext.Trim(),
                Verwendungszweck = t.Verwendungszweck?.Trim() ?? string.Empty,
                Betrag = t.Betrag,
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
