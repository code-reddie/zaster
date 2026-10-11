using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Zaster.Common;
using Zaster.Database;
using Zaster.FinTs;

namespace Zaster.Backup;

[Route("api/backup")]
public sealed class BackupController(AppDbContext context, NasBackup nasBackup, TimeProvider timeProvider) : ApiControllerBase
{
    private const string Forbidden = "Die Sicherung darf nur verwalten, wer Zaster eingerichtet hat.";

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

    /// <summary>
    /// Zielordner, Uhrzeit und Stand der automatischen Sicherung.
    /// </summary>
    [HttpGet("schedule")]
    public async Task<ActionResult<BackupScheduleDto>> GetSchedule(CancellationToken cancellationToken)
    {
        if (!await DatabaseBackup.IsAllowedAsync(context, UserId, cancellationToken))
        {
            return Problem(detail: Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        return await nasBackup.GetScheduleAsync(cancellationToken);
    }

    /// <summary>
    /// Stellt die Uhrzeit der täglichen Sicherung ein. Leer = keine automatische Sicherung.
    /// </summary>
    [HttpPut("schedule")]
    public async Task<ActionResult<BackupScheduleDto>> SaveSchedule(SaveBackupSchedule request, CancellationToken cancellationToken)
    {
        if (!await DatabaseBackup.IsAllowedAsync(context, UserId, cancellationToken))
        {
            return Problem(detail: Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        string? time = null;
        if (!string.IsNullOrWhiteSpace(request.Time))
        {
            if (SyncTimes.Normalize([request.Time]) is not [var normalized])
            {
                return BadRequestProblem("Die Uhrzeit muss im Format HH:mm angegeben werden.");
            }

            time = normalized;
        }

        var settings = await nasBackup.GetSettingsAsync(cancellationToken);
        settings.Time = time;
        await context.SaveChangesAsync(cancellationToken);
        return await nasBackup.GetScheduleAsync(cancellationToken);
    }

    /// <summary>
    /// Sichert sofort in den Zielordner, z. B. um die Einrichtung zu prüfen. Das Ergebnis steht im zurückgegebenen Stand.
    /// </summary>
    [HttpPost("schedule/run")]
    public async Task<ActionResult<BackupScheduleDto>> RunNow(CancellationToken cancellationToken)
    {
        if (!await DatabaseBackup.IsAllowedAsync(context, UserId, cancellationToken))
        {
            return Problem(detail: Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        if (nasBackup.Directory is null)
        {
            return BadRequestProblem("Kein Zielordner eingestellt (Backup__Directory).");
        }

        await nasBackup.RunAsync(cancellationToken);
        return await nasBackup.GetScheduleAsync(cancellationToken);
    }
}
