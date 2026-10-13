using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Common;
using Zaster.Database;

namespace Zaster.Accounts;

[Route("api/accounts")]
public sealed class AccountsController(AppDbContext context) : ApiControllerBase
{
    /// <summary>
    /// Die eigenen Konten in der Reihenfolge, die der Nutzer unter Einstellungen festgelegt hat.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<AccountDto>> GetAccounts(CancellationToken cancellationToken)
    {
        var accounts = await ToDtosAsync(context.Accounts.Where(a => a.Users.Any(u => u.Id == UserId)), cancellationToken);
        var order = await context.Users
            .Where(u => u.Id == UserId)
            .Select(u => u.AccountOrder)
            .SingleAsync(cancellationToken);
        return Sort(accounts, order);
    }

    /// <summary>
    /// Legt die eigene Reihenfolge der Konten fest. Die anderen Mitglieder geteilter Konten behalten ihre.
    /// </summary>
    [HttpPut("order")]
    public async Task<IActionResult> Reorder(ReorderAccounts request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Include(u => u.Accounts)
            .SingleAsync(u => u.Id == UserId, cancellationToken);
        if (!user.Accounts.Select(a => a.Id).Order().SequenceEqual(request.Ids.Order()))
        {
            return BadRequestProblem("Die Liste passt nicht zu deinen Konten. Lade die Seite neu und versuche es noch einmal.");
        }

        user.AccountOrder = [.. request.Ids];
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Sortiert nach der eigenen Reihenfolge; Konten, die darin fehlen, kommen dahinter und behalten
    /// ihre bisherige Reihenfolge (nach Name).
    /// </summary>
    internal static IReadOnlyList<AccountDto> Sort(IReadOnlyList<AccountDto> accounts, IReadOnlyList<int> order)
    {
        var positions = order.Select((id, index) => (id, index)).ToDictionary(p => p.id, p => p.index);
        return accounts
            .OrderBy(a => positions.TryGetValue(a.Id, out var index) ? index : int.MaxValue)
            .ToList();
    }

    [HttpGet("{accountId:int}")]
    [AccountMember]
    public async Task<AccountDto> GetAccount(int accountId, CancellationToken cancellationToken) =>
        (await ToDtosAsync(context.Accounts.Where(a => a.Id == accountId), cancellationToken)).Single();

    private const string AccountExists = "Ein Konto mit dieser IBAN gibt es in Zaster schon.";

    [HttpPost]
    public async Task<ActionResult<AccountDto>> CreateAccount(CreateAccount request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequestProblem("Bitte gib dem Konto einen Namen.");
        }

        var iban = Iban.Normalize(request.Iban);
        if (iban is null)
        {
            return BadRequestProblem("Bitte gib eine gültige IBAN ein.");
        }

        if (await context.Accounts.AnyAsync(a => a.Iban == iban, cancellationToken))
        {
            return ConflictProblem(AccountExists);
        }

        var user = await context.Users.SingleAsync(u => u.Id == UserId, cancellationToken);
        var account = new Account { Name = request.Name.Trim(), Iban = iban, Users = [user] };
        context.Accounts.Add(account);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            return ConflictProblem(AccountExists);
        }

        return await GetAccount(account.Id, cancellationToken);
    }

    [HttpPut("{accountId:int}")]
    [AccountMember]
    public async Task<ActionResult<AccountDto>> UpdateAccount(int accountId, UpdateAccount request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequestProblem("Bitte gib dem Konto einen Namen.");
        }

        var account = await context.Accounts.SingleAsync(a => a.Id == accountId, cancellationToken);
        account.Name = request.Name.Trim();
        account.FinTsUserId = string.IsNullOrWhiteSpace(request.FinTsUserId) ? null : request.FinTsUserId.Trim();
        await context.SaveChangesAsync(cancellationToken);

        return await GetAccount(accountId, cancellationToken);
    }

    /// <summary>
    /// Löscht das Konto mit allen Buchungen, Kategorien und Regeln, auch für die anderen Mitglieder.
    /// </summary>
    [HttpDelete("{accountId:int}")]
    [AccountMember]
    public async Task<IActionResult> DeleteAccount(int accountId, CancellationToken cancellationToken)
    {
        await context.Accounts.Where(a => a.Id == accountId).ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Teilt das Konto mit einem schon registrierten Nutzer. Er ist sofort Mitglied, mit denselben Rechten wie alle anderen.
    /// </summary>
    [HttpPost("{accountId:int}/members")]
    [AccountMember]
    public async Task<ActionResult<AccountDto>> AddMember(int accountId, AddMember request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return BadRequestProblem("Bitte gib den Namen ein, mit dem sich die Person bei Zaster anmeldet.");
        }

        var user = await context.Users.FirstOrDefaultAsync(u => u.Name == name, cancellationToken);
        if (user is null)
        {
            return BadRequestProblem($"„{name}“ gibt es in Zaster nicht. Die Person muss sich erst registrieren; achte auf Groß- und Kleinschreibung.");
        }

        var account = await context.Accounts
            .Include(a => a.Users)
            .SingleAsync(a => a.Id == accountId, cancellationToken);
        if (account.Users.Any(u => u.Id == user.Id))
        {
            return ConflictProblem($"{user.Name} ist schon Mitglied.");
        }

        account.Users.Add(user);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            // Jemand anderes hat dieselbe Person gerade gleichzeitig hinzugefügt.
            return ConflictProblem($"{user.Name} ist schon Mitglied.");
        }

        return await GetAccount(accountId, cancellationToken);
    }

    /// <summary>
    /// Entfernt ein Mitglied; mit der eigenen Id verlässt man das Konto. Das letzte Mitglied kann nicht gehen,
    /// sonst bliebe ein Konto übrig, das niemand mehr sieht; es kann das Konto aber löschen.
    /// </summary>
    [HttpDelete("{accountId:int}/members/{userId:int}")]
    [AccountMember]
    public async Task<IActionResult> RemoveMember(int accountId, int userId, CancellationToken cancellationToken)
    {
        var account = await context.Accounts
            .Include(a => a.Users)
            .SingleAsync(a => a.Id == accountId, cancellationToken);
        var member = account.Users.FirstOrDefault(u => u.Id == userId);
        if (member is null)
        {
            return NotFoundProblem("Diese Person ist kein Mitglied des Kontos.");
        }

        if (account.Users.Count == 1)
        {
            return ConflictProblem("Du bist das einzige Mitglied. Lösche das Konto, wenn du es nicht mehr brauchst.");
        }

        account.Users.Remove(member);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<List<AccountDto>> ToDtosAsync(IQueryable<Account> accounts, CancellationToken cancellationToken)
    {
        var dtos = await ToDtos(accounts).ToListAsync(cancellationToken);
        var ids = dtos.Select(a => a.Id).ToList();

        // Eigene Abfrage: Distinct innerhalb der Projektion braucht APPLY, das kann SQLite nicht.
        var anlageklassen = (await context.Transactions
            .Where(t => ids.Contains(t.AccountId) && t.Anlageklasse != null)
            .Select(t => new { t.AccountId, Anlageklasse = t.Anlageklasse! })
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToLookup(t => t.AccountId, t => t.Anlageklasse);

        return dtos
            .Select(a => a with { Anlageklassen = anlageklassen[a.Id].Order(StringComparer.Ordinal).ToList() })
            .ToList();
    }

    private static IQueryable<AccountDto> ToDtos(IQueryable<Account> accounts) =>
        accounts
            .OrderBy(a => a.Name)
            .Select(a => new AccountDto(
                a.Id,
                a.Name,
                a.Iban,
                a.FinTsUserId,
                a.FinTsPin != null,
                a.SyncTimes,
                a.LastSyncedAt,
                a.LastSyncError,
                a.Users.OrderBy(u => u.Name).Select(u => new AccountMemberDto(u.Id, u.Name)).ToList(),
                a.Transactions.Count(t => !t.Splits.Any() && !t.Reviewed),
                Array.Empty<string>()));
}
