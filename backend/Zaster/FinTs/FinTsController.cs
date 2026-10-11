using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Common;
using Zaster.Database;

namespace Zaster.FinTs;

[Route("api/accounts/{accountId:int}/fints")]
[AccountMember]
public sealed class FinTsController(AppDbContext context, AccountSyncService syncService) : ApiControllerBase
{
    /// <summary>
    /// Ruft die Umsätze des Kontos bei der Bank ab und übernimmt die neuen.
    /// Ohne PIN im Request wird die gespeicherte verwendet.
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<FinTsSyncResult>> Sync(int accountId, FinTsSyncRequest request, CancellationToken cancellationToken)
    {
        var account = await context.Accounts.SingleAsync(a => a.Id == accountId, cancellationToken);
        var result = await syncService.SyncAsync(account, request.UserId, request.Pin, request.SavePin, cancellationToken);
        return result is null ? BadRequestProblem("Bitte gib deine PIN ein.") : result;
    }

    /// <summary>
    /// Löscht die gespeicherte PIN. Der automatische Abruf ist danach für dieses Konto aus.
    /// </summary>
    [HttpDelete("pin")]
    public async Task<IActionResult> DeletePin(int accountId, CancellationToken cancellationToken)
    {
        await context.Accounts
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.FinTsPin, (string?)null), cancellationToken);
        return NoContent();
    }
}
