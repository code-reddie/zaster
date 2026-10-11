using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Backup;
using Zaster.Common;
using Zaster.Database;

namespace Zaster.Users;

[Route("api/me")]
public sealed class MeController(AppDbContext context) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserDto>> GetMe(CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == UserId, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return new UserDto(user.Id, user.Name, await DatabaseBackup.IsAllowedAsync(context, user.Id, cancellationToken));
    }

    /// <summary>
    /// Löscht den eigenen Zugang. Konten, die niemand sonst nutzt, werden mit allen Buchungen gelöscht;
    /// geteilte Konten bleiben den anderen Mitgliedern.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Include(u => u.Accounts)
            .ThenInclude(a => a.Users)
            .FirstOrDefaultAsync(u => u.Id == UserId, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        context.Accounts.RemoveRange(user.Accounts.Where(a => a.Users.Count == 1));
        context.Users.Remove(user);
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
