using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Zaster.Common;
using Zaster.Database;

namespace Zaster.Backup;

[Route("api/backup")]
public sealed class BackupController(AppDbContext context, TimeProvider timeProvider) : ApiControllerBase
{
    /// <summary>
    /// Lädt die komplette Datenbank als SQLite-Datei herunter (nur für den Nutzer, der Zaster eingerichtet hat).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Download(CancellationToken cancellationToken)
    {
        if (!await DatabaseBackup.IsAllowedAsync(context, UserId, cancellationToken))
        {
            return Problem(
                detail: "Die Sicherung darf nur herunterladen, wer Zaster eingerichtet hat.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var path = Path.Combine(Path.GetTempPath(), $"zaster-sicherung-{Guid.NewGuid():N}.db");
        try
        {
            await DatabaseBackup.WriteAsync(context, path, cancellationToken);
        }
        catch
        {
            System.IO.File.Delete(path);
            throw;
        }

        // Die Kopie verschwindet, sobald sie ausgeliefert (oder der Download abgebrochen) ist.
        var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        var date = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return File(stream, "application/vnd.sqlite3", $"zaster-{date:yyyy-MM-dd}.db");
    }
}
