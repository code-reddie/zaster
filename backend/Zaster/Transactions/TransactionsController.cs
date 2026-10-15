using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Categories;
using Zaster.Common;
using Zaster.Database;

namespace Zaster.Transactions;

[Route("api/accounts/{accountId:int}/transactions")]
[AccountMember]
public sealed class TransactionsController(AppDbContext context, TransactionImporter importer, TimeProvider timeProvider) : ApiControllerBase
{
    private const int MaxLimit = 500;

    private static readonly string TooLong = $"Die Bemerkung darf höchstens {Transaction.MaxBemerkung} Zeichen haben.";

    /// <summary>
    /// Buchungen des Kontos, neueste zuerst, gefiltert und seitenweise. Einnahmen und Ausgaben
    /// beziehen sich auf alle Treffer, nicht nur auf die Seite; mit Kategorie-Filter zählen bei
    /// aufgeteilten Buchungen nur die Teilbeträge dieser Kategorie (und ihrer Unterkategorien).
    /// </summary>
    [HttpGet]
    public async Task<TransactionPage> GetTransactions(
        int accountId,
        [FromQuery] TransactionQuery query,
        CancellationToken cancellationToken)
    {
        var transactions = context.Transactions.Where(t => t.AccountId == accountId);

        if (query.From is { } from)
        {
            transactions = transactions.Where(t => t.Buchung >= from);
        }

        if (query.To is { } to)
        {
            transactions = transactions.Where(t => t.Buchung <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var pattern = $"%{EscapeLike(search)}%";

            // Sieht der Begriff wie ein Betrag aus, findet er zusätzlich Buchungen mit genau diesem
            // Betrag oder Teilbetrag; ohne Vorzeichen Einnahmen und Ausgaben.
            var isAmount = AmountSearch.TryParse(search, out var amount, out var signed);
            var amountA = amount;
            var amountB = signed ? amount : -amount;

            transactions = transactions.Where(t =>
                EF.Functions.Like(t.Auftraggeber, pattern, "\\")
                || EF.Functions.Like(t.Buchungstext, pattern, "\\")
                || EF.Functions.Like(t.Verwendungszweck, pattern, "\\")
                || (t.Bemerkung != null && EF.Functions.Like(t.Bemerkung, pattern, "\\"))
                || (isAmount && (t.Betrag == amountA || t.Betrag == amountB
                    || t.Splits.Any(s => s.Betrag == amountA || s.Betrag == amountB))));
        }

        HashSet<int>? categoryIds = null;
        if (query.CategoryId is { } categoryId)
        {
            var categories = await context.Categories.Where(c => c.AccountId == accountId).ToListAsync(cancellationToken);
            var ids = CategoryTree.WithDescendants(categories, categoryId);
            categoryIds = ids;
            transactions = transactions.Where(t => t.Splits.Any(s => ids.Contains(s.CategoryId)));
        }

        if (query.WithoutCategory)
        {
            transactions = transactions.Where(t => !t.Splits.Any());
        }

        if (query.Open)
        {
            transactions = transactions.Where(t => !t.Splits.Any() && !t.Reviewed);
        }

        var total = await transactions.CountAsync(cancellationToken);

        // SQLite rechnet nicht mit decimal, die Summen entstehen daher im Speicher.
        var amounts = categoryIds is null
            ? await transactions.Select(t => t.Betrag).ToListAsync(cancellationToken)
            : await transactions
                .SelectMany(t => t.Splits)
                .Where(s => categoryIds.Contains(s.CategoryId))
                .Select(s => s.Betrag)
                .ToListAsync(cancellationToken);

        var items = await transactions
            .Include(t => t.Splits)
            .OrderByDescending(t => t.Buchung)
            .ThenByDescending(t => t.Id)
            .Skip(Math.Max(query.Offset, 0))
            .Take(Math.Clamp(query.Limit, 1, MaxLimit))
            .ToListAsync(cancellationToken);

        return new TransactionPage(
            items.Select(TransactionDto.From).ToList(),
            total,
            amounts.Where(a => a > 0).Sum(),
            amounts.Where(a => a < 0).Sum());
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> CreateTransaction(
        int accountId,
        CreateTransaction request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Auftraggeber))
        {
            return BadRequestProblem("Bitte gib einen Auftraggeber oder Empfänger an.");
        }

        var bemerkung = Transaction.NormalizeBemerkung(request.Bemerkung);
        if (bemerkung?.Length > Transaction.MaxBemerkung)
        {
            return BadRequestProblem(TooLong);
        }

        var transaction = new Transaction
        {
            AccountId = accountId,
            Buchung = request.Buchung,
            Valuta = request.Valuta ?? request.Buchung,
            Auftraggeber = request.Auftraggeber.Trim(),
            Buchungstext = request.Buchungstext?.Trim() ?? string.Empty,
            Verwendungszweck = request.Verwendungszweck?.Trim() ?? string.Empty,
            Betrag = request.Betrag,
            Bemerkung = bemerkung,
            Source = TransactionSource.Manuell,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        var rules = await context.Rules.Include(r => r.Conditions).Where(r => r.AccountId == accountId).ToListAsync(cancellationToken);
        RuleEngine.Apply([transaction], rules);

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);

        return TransactionDto.From(transaction);
    }

    /// <summary>
    /// Übernimmt Buchungen aus einer CSV-Datei und überspringt solche, die schon im Konto liegen.
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<ImportResult>> Import(int accountId, ImportTransactions request, CancellationToken cancellationToken)
    {
        if (TransactionImporter.Validate(request.Transactions) is { } error)
        {
            return BadRequestProblem(error);
        }

        return await importer.ImportAsync(accountId, request.Transactions, TransactionSource.Csv, cancellationToken);
    }

    /// <summary>
    /// Prüft anhand der Kontostände der Bank, ob Buchungen fehlen, und liefert den letzten bekannten Kontostand.
    /// </summary>
    [HttpGet("balance-check")]
    public async Task<BalanceCheckDto> CheckBalance(int accountId, CancellationToken cancellationToken)
    {
        var entries = await context.Transactions
            .Where(t => t.AccountId == accountId)
            .Select(t => new BalanceCheck.Entry(t.Buchung, t.Betrag, t.Saldo, t.Source))
            .ToListAsync(cancellationToken);

        return BalanceCheck.Check(entries);
    }

    /// <summary>
    /// Setzt eine Kategorie für den ganzen Betrag (ersetzt eine Aufteilung) oder entfernt alle Kategorien.
    /// Mit Kategorie gilt die Buchung als geprüft.
    /// </summary>
    [HttpPut("{id:int}/category")]
    public async Task<ActionResult<TransactionDto>> SetCategory(
        int accountId,
        int id,
        SetCategory request,
        CancellationToken cancellationToken)
    {
        var transaction = await FindAsync(accountId, id, cancellationToken);
        if (transaction is null)
        {
            return NotFoundProblem("Die Buchung gibt es nicht.");
        }

        if (request.CategoryId is { } categoryId
            && !await context.Categories.AnyAsync(c => c.Id == categoryId && c.AccountId == accountId, cancellationToken))
        {
            return BadRequestProblem("Die Kategorie gibt es in diesem Konto nicht.");
        }

        transaction.Categorize(request.CategoryId);
        await context.SaveChangesAsync(cancellationToken);
        return TransactionDto.From(transaction);
    }

    /// <summary>
    /// Teilt die Buchung auf mehrere Kategorien auf, z. B. Lebensmittel und eine Bohrmaschine im selben Einkauf.
    /// Die Teilbeträge haben das Vorzeichen der Buchung und ergeben zusammen genau ihren Betrag.
    /// </summary>
    [HttpPut("{id:int}/splits")]
    public async Task<ActionResult<TransactionDto>> SetSplits(
        int accountId,
        int id,
        SetSplits request,
        CancellationToken cancellationToken)
    {
        var transaction = await FindAsync(accountId, id, cancellationToken);
        if (transaction is null)
        {
            return NotFoundProblem("Die Buchung gibt es nicht.");
        }

        var parts = request.Splits ?? [];
        if (Transaction.ValidateSplit(transaction.Betrag, parts) is { } error)
        {
            return BadRequestProblem(error);
        }

        var categoryIds = parts.Select(p => p.CategoryId).ToList();
        var known = await context.Categories.CountAsync(c => c.AccountId == accountId && categoryIds.Contains(c.Id), cancellationToken);
        if (known != categoryIds.Count)
        {
            return BadRequestProblem("Die Kategorie gibt es in diesem Konto nicht.");
        }

        transaction.Split(parts);
        await context.SaveChangesAsync(cancellationToken);
        return TransactionDto.From(transaction);
    }

    /// <summary>
    /// Hakt eine Buchung ab oder öffnet sie wieder. Buchungen mit Kategorie bleiben immer geprüft.
    /// </summary>
    [HttpPut("{id:int}/reviewed")]
    public async Task<ActionResult<TransactionDto>> SetReviewed(
        int accountId,
        int id,
        SetReviewed request,
        CancellationToken cancellationToken)
    {
        var transaction = await FindAsync(accountId, id, cancellationToken);
        if (transaction is null)
        {
            return NotFoundProblem("Die Buchung gibt es nicht.");
        }

        transaction.Reviewed = request.Reviewed || transaction.IsCategorized;
        await context.SaveChangesAsync(cancellationToken);
        return TransactionDto.From(transaction);
    }

    /// <summary>
    /// Setzt die eigene Bemerkung zur Buchung oder entfernt sie (leer). Ändert nichts an Kategorie oder „geprüft“.
    /// </summary>
    [HttpPut("{id:int}/bemerkung")]
    public async Task<ActionResult<TransactionDto>> SetBemerkung(
        int accountId,
        int id,
        SetBemerkung request,
        CancellationToken cancellationToken)
    {
        var bemerkung = Transaction.NormalizeBemerkung(request.Bemerkung);
        if (bemerkung?.Length > Transaction.MaxBemerkung)
        {
            return BadRequestProblem(TooLong);
        }

        var transaction = await FindAsync(accountId, id, cancellationToken);
        if (transaction is null)
        {
            return NotFoundProblem("Die Buchung gibt es nicht.");
        }

        transaction.Bemerkung = bemerkung;
        await context.SaveChangesAsync(cancellationToken);
        return TransactionDto.From(transaction);
    }

    /// <summary>
    /// „Alle als geprüft markieren“: hakt die angegebenen oder alle offenen Buchungen des Kontos ab.
    /// </summary>
    [HttpPost("review")]
    public async Task<ReviewResult> Review(int accountId, ReviewTransactions request, CancellationToken cancellationToken)
    {
        var open = context.Transactions.Where(t => t.AccountId == accountId && !t.Reviewed);
        if (request.Ids is { } ids)
        {
            open = open.Where(t => ids.Contains(t.Id));
        }

        var previous = await open.Include(t => t.Splits).ToListAsync(cancellationToken);
        foreach (var transaction in previous)
        {
            transaction.Reviewed = true;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Offen heißt ohne Kategorie und nicht geprüft; nur „geprüft“ hat sich geändert.
        var states = previous.Select(t => TransactionState.From(t) with { Reviewed = false }).ToList();
        return new ReviewResult(states.Count, states);
    }

    /// <summary>
    /// „Rückgängig“: setzt Kategorien, „geprüft“ und Bemerkung der Buchungen auf den mitgeschickten Zustand
    /// von vor einer Aktion. Inzwischen gelöschte Buchungen werden übersprungen. Liefert die Buchungen danach.
    /// </summary>
    [HttpPost("restore")]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> Restore(
        int accountId,
        RestoreTransactions request,
        CancellationToken cancellationToken)
    {
        var states = request.States ?? [];
        if (states.Select(s => s.Id).Distinct().Count() != states.Count)
        {
            return BadRequestProblem("Jede Buchung darf nur einmal vorkommen.");
        }

        var ids = states.Select(s => s.Id).ToList();
        var transactions = await context.Transactions
            .Include(t => t.Splits)
            .Where(t => t.AccountId == accountId && ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var categoryIds = states.SelectMany(s => s.Splits ?? []).Select(p => p.CategoryId).ToHashSet();
        var known = await context.Categories
            .Where(c => c.AccountId == accountId && categoryIds.Contains(c.Id))
            .CountAsync(cancellationToken);
        if (known != categoryIds.Count)
        {
            return BadRequestProblem("Eine der Kategorien gibt es nicht mehr, das lässt sich nicht mehr rückgängig machen.");
        }

        var restored = new List<Transaction>();
        foreach (var state in states)
        {
            if (!transactions.TryGetValue(state.Id, out var transaction))
            {
                continue;
            }

            var parts = state.Splits ?? [];
            if (Transaction.ValidateSplit(transaction.Betrag, parts) is { } error)
            {
                return BadRequestProblem(error);
            }

            var bemerkung = Transaction.NormalizeBemerkung(state.Bemerkung);
            if (bemerkung?.Length > Transaction.MaxBemerkung)
            {
                return BadRequestProblem(TooLong);
            }

            transaction.Split(parts);
            transaction.Reviewed = state.Reviewed || transaction.IsCategorized;
            transaction.Bemerkung = bemerkung;
            restored.Add(transaction);
        }

        await context.SaveChangesAsync(cancellationToken);
        return restored.Select(TransactionDto.From).ToList();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTransaction(int accountId, int id, CancellationToken cancellationToken)
    {
        var deleted = await context.Transactions
            .Where(t => t.Id == id && t.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? NotFoundProblem("Die Buchung gibt es nicht.") : NoContent();
    }

    private Task<Transaction?> FindAsync(int accountId, int id, CancellationToken cancellationToken) =>
        context.Transactions
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == id && t.AccountId == accountId, cancellationToken);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
