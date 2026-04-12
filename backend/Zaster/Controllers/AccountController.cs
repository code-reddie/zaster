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
public sealed class AccountController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    [HttpPost]
    public async Task<ActionResult<Account>> CreateAccount(
        CreateAccount dto,
        CancellationToken cancellationToken)
    {
        var account = new Account
        {
            Id = 0,
            Name = dto.Name
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, account);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Account>> GetAccount(
        int id,
        CancellationToken cancellationToken)
    {
        var account = await _context.Accounts.FindAsync(id, cancellationToken);

        if (account == null)
        {
            return NotFound();
        }

        return account;
    }

    [HttpGet("user/{userName}")]
    public async Task<ActionResult<IEnumerable<Account>>> GetAccounts(
        string userName,
        CancellationToken cancellationToken)
    {
        var accounts = await _context.Accounts
            .Where(a => a.Users.Any(u => u.Name == userName))
            .ToListAsync(cancellationToken);

        return accounts;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAccount(
        int id,
        CancellationToken cancellationToken)
    {
        var account = await _context.Accounts.FindAsync(id, cancellationToken);

        if (account == null)
        {
            return NotFound();
        }

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
