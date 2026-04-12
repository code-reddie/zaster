using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Database;
using Zaster.Models;

namespace Zaster.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TransactionsController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    [HttpPost]
    public async Task<ActionResult<Transaction>> CreateTransaction(
        CreateTransaction dto,
        CancellationToken cancellationToken)
    {
        var account = await _context.Accounts.FindAsync(dto.AccountId, cancellationToken);
        if (account == null)
        {
            return BadRequest($"Account with ID {dto.AccountId} does not exist.");
        }

        var transaction = new Transaction
        {
            Id = 0,
            Buchung = dto.Buchung,
            Valuta = dto.Valuta,
            Auftragsgeber = dto.Auftragsgeber,
            Buchungstext = dto.Buchungstext,
            Verwendungszweck = dto.Verwendungszweck,
            Betrag = dto.Betrag,
            Account = account
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Transaction>> GetTransaction(
        int id,
        CancellationToken cancellationToken)
    {
        var transaction = await _context.Transactions.FindAsync(id, cancellationToken);

        if (transaction == null)
        {
            return NotFound();
        }

        return transaction;
    }

    [HttpGet("account/{accountId}")]
    public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions(
        int accountId,
        CancellationToken cancellationToken)
    {
        var transactions = await _context.Transactions
            .Where(t => t.AccountId == accountId)
            .ToListAsync(cancellationToken);

        return transactions
            .OrderByDescending(t => t.Buchung)
            .ToList();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(
        int id,
        CancellationToken cancellationToken)
    {
        var transaction = await _context.Transactions.FindAsync(id, cancellationToken);

        if (transaction == null)
        {
            return NotFound();
        }

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
