using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Zaster.Categorization;
using Zaster.Database;
using Zaster.Models;

namespace Zaster.Import;

/// <summary>
/// Legt Buchungen in einem Konto an und überspringt solche, die dort schon liegen.
/// </summary>
public sealed class TransactionImporter(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<ImportTransactionsResult> ImportAsync(
        Account account,
        int userId,
        IReadOnlyList<ImportTransaction> incoming,
        CancellationToken cancellationToken)
    {
        // SQLite kann DateTimeOffset nicht vergleichen, daher wird im Speicher abgeglichen.
        var existing = await _context.Transactions
            .Where(t => t.AccountId == account.Id)
            .Select(t => new { t.Buchung, t.Betrag, t.Auftragsgeber })
            .ToListAsync(cancellationToken);

        var newItems = TransactionMatcher.SelectNew(
            existing.Select(t => TransactionMatcher.Key(t.Buchung, t.Betrag, t.Auftragsgeber)),
            incoming,
            t => TransactionMatcher.Key(t.Buchung, t.Betrag, t.Auftragsgeber));

        var transactions = newItems
            .Select(t => new Transaction
            {
                Buchung = t.Buchung,
                Valuta = t.Valuta,
                Auftragsgeber = t.Auftragsgeber.Trim(),
                Buchungstext = t.Buchungstext.Trim(),
                Verwendungszweck = t.Verwendungszweck?.Trim() ?? string.Empty,
                Betrag = t.Betrag,
                AccountId = account.Id,
            })
            .ToList();

        var rules = await _context.CategorizationRules
            .Where(r => r.Category!.UserId == userId)
            .ToListAsync(cancellationToken);
        RuleEngine.Apply(transactions, rules);

        _context.Transactions.AddRange(transactions);
        await _context.SaveChangesAsync(cancellationToken);

        return new ImportTransactionsResult(
            transactions.Count,
            incoming.Count - transactions.Count,
            transactions
                .Select(t => new TransactionDto(
                    t.Id,
                    t.Buchung,
                    t.Valuta,
                    t.Auftragsgeber,
                    t.Buchungstext,
                    t.Verwendungszweck,
                    t.Betrag,
                    t.AccountId,
                    t.CategoryId))
                .ToList());
    }
}
