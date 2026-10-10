using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Database;
using Zaster.FinTs;
using Zaster.Models;

namespace Zaster.Controllers;

[ApiController]
[Route("api/account/{id}/fints")]
public sealed class AccountFinTsController(AppDbContext context, AccountSyncService syncService) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly AccountSyncService _syncService = syncService;

    private int? GetUserId()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdString, out var userId) ? userId : null;
    }

    /// <summary>
    /// Ruft die Umsätze des Kontos bei der Bank ab und übernimmt die neuen.
    /// Ohne PIN im Request wird die gespeicherte verwendet.
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<FinTsSyncResult>> Sync(
        int id,
        FinTsSyncRequest request,
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

        var result = await _syncService.SyncAsync(
            account,
            userId.Value,
            request.UserId,
            request.Pin,
            request.SavePin,
            cancellationToken);

        return result is null ? BadRequest("Bitte gib deine PIN ein.") : Ok(result);
    }

    /// <summary>
    /// Löscht die gespeicherte PIN.
    /// </summary>
    [HttpDelete("pin")]
    public async Task<ActionResult<AccountDto>> DeletePin(int id, CancellationToken cancellationToken)
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

        account.FinTsPin = null;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(AccountDto.From(account));
    }
}
