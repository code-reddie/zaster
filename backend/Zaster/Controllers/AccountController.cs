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
public sealed class AccountController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    private int? GetUserId()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdString, out var userId) ? userId : null;
    }

    [HttpPost]
    public async Task<ActionResult<Account>> CreateAccount(
        CreateAccount dto,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync([GetUserId()], cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var account = new Account
        {
            Name = dto.Name,
            Users = [user]
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);

        var result = new AccountDto(account.Id, account.Name);

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Account>>> GetAccounts(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var accounts = await _context.Accounts
            .Where(a => a.Users.Any(u => u.Id == userId))
            .ToListAsync(cancellationToken);

        var result = accounts.Select(a => new AccountDto(a.Id, a.Name));
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAccount(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.Users.Any(u => u.Id == userId), cancellationToken);

        if (account == null)
        {
            return NotFound();
        }

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
