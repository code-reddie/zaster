using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Database;
using Zaster.Models;

namespace Zaster.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TransactionController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

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
