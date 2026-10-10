using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Database;
using Zaster.FinTs;
using Zaster.Import;
using Zaster.Models;

namespace Zaster.Controllers;

[ApiController]
[Route("api/account/{id}/fints")]
public sealed class AccountFinTsController(
    AppDbContext context,
    FinTsService finTsService,
    FinTsPinProtector pinProtector,
    TransactionImporter importer) : ControllerBase
{
    private const int MaxDays = 90;

    // Überlappung zum letzten Abruf, damit nachträglich gebuchte Umsätze nicht fehlen.
    private const int OverlapDays = 7;

    private readonly AppDbContext _context = context;
    private readonly FinTsService _finTsService = finTsService;
    private readonly FinTsPinProtector _pinProtector = pinProtector;
    private readonly TransactionImporter _importer = importer;

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

        var enteredPin = string.IsNullOrWhiteSpace(request.Pin) ? null : request.Pin;
        var pin = enteredPin ?? (account.FinTsPin is null ? null : _pinProtector.Unprotect(account.FinTsPin));
        if (pin is null)
        {
            if (account.FinTsPin is not null)
            {
                // Der Schlüssel zum Entschlüsseln fehlt, z. B. nach einem neuen Container ohne /data/keys.
                account.FinTsPin = null;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return BadRequest("Bitte gib deine PIN ein.");
        }

        var userIdForBank = string.IsNullOrWhiteSpace(request.UserId) ? account.FinTsUserId : request.UserId.Trim();
        var days = account.LastSyncedAt is { } lastSynced
            ? Math.Clamp((DateTimeOffset.UtcNow - lastSynced).Days + OverlapDays, 1, MaxDays)
            : MaxDays;

        var fetched = await _finTsService.FetchTransactionsAsync(
            new FinTsTestRequest(account.Iban, userIdForBank, pin, days),
            cancellationToken);

        if (!fetched.Success)
        {
            // Mehrere Fehlversuche mit einer falschen PIN sperren den Zugang bei der Bank.
            // Eine abgelehnte gespeicherte PIN wird daher nicht noch einmal verwendet.
            if (fetched.LoginRejected && enteredPin is null)
            {
                account.FinTsPin = null;
                await _context.SaveChangesAsync(cancellationToken);
                return Ok(Failed(fetched, account, "Die ING hat die Anmeldung mit der gespeicherten PIN abgelehnt. Die PIN wurde gelöscht, bitte gib sie neu ein."));
            }

            return Ok(Failed(fetched, account, fetched.Error));
        }

        if (enteredPin is not null && request.SavePin)
        {
            account.FinTsPin = _pinProtector.Protect(enteredPin);
        }

        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            account.FinTsUserId = request.UserId.Trim();
        }

        account.LastSyncedAt = DateTimeOffset.UtcNow;

        var imported = await _importer.ImportAsync(
            account,
            userId.Value,
            fetched.Transactions
                .Select(t => new ImportTransaction(t.Buchung, t.Valuta, t.Auftragsgeber, t.Buchungstext, t.Verwendungszweck, t.Betrag))
                .ToList(),
            cancellationToken);

        return Ok(new FinTsSyncResult(
            true,
            null,
            fetched.Messages,
            fetched.Diagnostics,
            imported.Imported,
            imported.Skipped,
            imported.Transactions,
            AccountDto.From(account)));
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

    private static FinTsSyncResult Failed(FinTsTestResult fetched, Account account, string? error) =>
        new(false, error, fetched.Messages, fetched.Diagnostics, 0, 0, [], AccountDto.From(account));
}
