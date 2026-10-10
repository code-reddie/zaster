using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Categorization;
using Zaster.Database;
using Zaster.Import;
using Zaster.Models;

namespace Zaster.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TransactionController(AppDbContext context, TransactionImporter importer) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly TransactionImporter _importer = importer;

    private int? GetUserId()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdString, out var userId) ? userId : null;
    }

    [HttpPost]
    public async Task<ActionResult<Transaction>> CreateTransaction(
        CreateTransaction dto,
        CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .Include(a => a.Users)
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId, cancellationToken);
        if (account == null)
        {
            return BadRequest($"Account with ID {dto.AccountId} does not exist.");
        }

        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!account.Users.Any(u => u.Id == userId))
        {
            return Forbid();
        }

        var transaction = new Transaction
        {
            Buchung = dto.Buchung,
            Valuta = dto.Valuta,
            Auftragsgeber = dto.Auftragsgeber,
            Buchungstext = dto.Buchungstext,
            Verwendungszweck = dto.Verwendungszweck ?? string.Empty,
            Betrag = dto.Betrag,
            Account = account
        };

        var rules = await _context.CategorizationRules
            .Where(r => r.Category!.UserId == userId)
            .ToListAsync(cancellationToken);
        RuleEngine.Apply([transaction], rules);

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        var result = new TransactionDto(
            transaction.Id,
            transaction.Buchung,
            transaction.Valuta,
            transaction.Auftragsgeber,
            transaction.Buchungstext,
            transaction.Verwendungszweck,
            transaction.Betrag,
            transaction.AccountId,
            transaction.CategoryId);

        return Ok(result);
    }


    /// <summary>
    /// Legt mehrere Buchungen an und überspringt solche, die schon im Konto liegen.
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<ImportTransactionsResult>> ImportTransactions(
        ImportTransactions dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.Users.Any(u => u.Id == userId), cancellationToken);
        if (account == null)
        {
            return BadRequest($"Account with ID {dto.AccountId} does not exist.");
        }

        return Ok(await _importer.ImportAsync(account, userId.Value, dto.Transactions, cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Transaction>>> GetAllTransactions(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var transactions = await _context.Transactions
            .Where(t => t.Account!.Users.Any(u => u.Id == userId))
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
            .ToListAsync(cancellationToken);

        return Ok(transactions);
    }

    [HttpPut("{id}/category")]
    public async Task<ActionResult<TransactionDto>> SetCategory(
        int id,
        SetTransactionCategory dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.Account!.Users.Any(u => u.Id == userId), cancellationToken);
        if (transaction == null)
        {
            return NotFound();
        }

        if (dto.CategoryId is int categoryId
            && !await _context.Categories.AnyAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken))
        {
            return BadRequest($"Category with ID {categoryId} does not exist.");
        }

        transaction.CategoryId = dto.CategoryId;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new TransactionDto(
            transaction.Id,
            transaction.Buchung,
            transaction.Valuta,
            transaction.Auftragsgeber,
            transaction.Buchungstext,
            transaction.Verwendungszweck,
            transaction.Betrag,
            transaction.AccountId,
            transaction.CategoryId));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var transaction = await _context.Transactions.FindAsync(id, cancellationToken);
        if (transaction == null)
        {
            return NotFound();
        }

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.Users.Any(u => u.Id == userId), cancellationToken);
        if (account == null)
        {
            return Forbid();
        }

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
