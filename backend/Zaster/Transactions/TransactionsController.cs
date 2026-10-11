using System;
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

    /// <summary>
    /// Buchungen des Kontos, neueste zuerst, gefiltert und seitenweise. Einnahmen und Ausgaben
    /// beziehen sich auf alle Treffer, nicht nur auf die Seite.
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
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            transactions = transactions.Where(t =>
                EF.Functions.Like(t.Auftraggeber, pattern, "\\")
                || EF.Functions.Like(t.Buchungstext, pattern, "\\")
                || EF.Functions.Like(t.Verwendungszweck, pattern, "\\"));
        }

        if (query.CategoryId is { } categoryId)
        {
            var categories = await context.Categories.Where(c => c.AccountId == accountId).ToListAsync(cancellationToken);
            var ids = CategoryTree.WithDescendants(categories, categoryId);
            transactions = transactions.Where(t => t.CategoryId != null && ids.Contains(t.CategoryId.Value));
        }

        if (query.WithoutCategory)
        {
            transactions = transactions.Where(t => t.CategoryId == null);
        }

        if (query.Open)
        {
            transactions = transactions.Where(t => t.CategoryId == null && !t.Reviewed);
        }

        // SQLite rechnet nicht mit decimal, die Summen entstehen daher im Speicher.
        var amounts = await transactions.Select(t => t.Betrag).ToListAsync(cancellationToken);

        var items = await transactions
            .OrderByDescending(t => t.Buchung)
            .ThenByDescending(t => t.Id)
            .Skip(Math.Max(query.Offset, 0))
            .Take(Math.Clamp(query.Limit, 1, MaxLimit))
            .ToListAsync(cancellationToken);

        return new TransactionPage(
            items.Select(TransactionDto.From).ToList(),
            amounts.Count,
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

        var transaction = new Transaction
        {
            AccountId = accountId,
            Buchung = request.Buchung,
            Valuta = request.Valuta ?? request.Buchung,
            Auftraggeber = request.Auftraggeber.Trim(),
            Buchungstext = request.Buchungstext?.Trim() ?? string.Empty,
            Verwendungszweck = request.Verwendungszweck?.Trim() ?? string.Empty,
            Betrag = request.Betrag,
            Source = TransactionSource.Manuell,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        var rules = await context.Rules.Where(r => r.AccountId == accountId).ToListAsync(cancellationToken);
        RuleEngine.Apply([transaction], rules);

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);

        return TransactionDto.From(transaction);
    }

    /// <summary>
    /// Übernimmt Buchungen aus einer CSV-Datei und überspringt solche, die schon im Konto liegen.
    /// </summary>
    [HttpPost("import")]
    public async Task<ImportResult> Import(int accountId, ImportTransactions request, CancellationToken cancellationToken) =>
        await importer.ImportAsync(accountId, request.Transactions, TransactionSource.Csv, cancellationToken);

    /// <summary>
    /// Setzt oder entfernt die Kategorie. Mit Kategorie gilt die Buchung als geprüft.
    /// </summary>
    [HttpPut("{id:int}/category")]
    public async Task<ActionResult<TransactionDto>> SetCategory(
        int accountId,
        int id,
        SetCategory request,
        CancellationToken cancellationToken)
    {
        var transaction = await context.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.AccountId == accountId, cancellationToken);
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
    /// Hakt eine Buchung ab oder öffnet sie wieder. Buchungen mit Kategorie bleiben immer geprüft.
    /// </summary>
    [HttpPut("{id:int}/reviewed")]
    public async Task<ActionResult<TransactionDto>> SetReviewed(
        int accountId,
        int id,
        SetReviewed request,
        CancellationToken cancellationToken)
    {
        var transaction = await context.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.AccountId == accountId, cancellationToken);
        if (transaction is null)
        {
            return NotFoundProblem("Die Buchung gibt es nicht.");
        }

        transaction.Reviewed = request.Reviewed || transaction.CategoryId is not null;
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

        var updated = await open.ExecuteUpdateAsync(s => s.SetProperty(t => t.Reviewed, true), cancellationToken);
        return new ReviewResult(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTransaction(int accountId, int id, CancellationToken cancellationToken)
    {
        var deleted = await context.Transactions
            .Where(t => t.Id == id && t.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? NotFoundProblem("Die Buchung gibt es nicht.") : NoContent();
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
