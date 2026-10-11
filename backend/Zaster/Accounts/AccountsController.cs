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
    [HttpGet]
    public async Task<IReadOnlyList<AccountDto>> GetAccounts(CancellationToken cancellationToken) =>
        await ToDtos(context.Accounts.Where(a => a.Users.Any(u => u.Id == UserId)))
            .ToListAsync(cancellationToken);

    [HttpGet("{accountId:int}")]
    [AccountMember]
    public async Task<AccountDto> GetAccount(int accountId, CancellationToken cancellationToken) =>
        await ToDtos(context.Accounts.Where(a => a.Id == accountId)).SingleAsync(cancellationToken);

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

        var user = await context.Users.SingleAsync(u => u.Id == UserId, cancellationToken);
        var account = new Account { Name = request.Name.Trim(), Iban = iban, Users = [user] };
        context.Accounts.Add(account);
        await context.SaveChangesAsync(cancellationToken);

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

    private static IQueryable<AccountDto> ToDtos(IQueryable<Account> accounts) =>
        accounts
            .OrderBy(a => a.Name)
            .Select(a => new AccountDto(
                a.Id,
                a.Name,
                a.Iban,
                a.FinTsUserId,
                a.FinTsPin != null,
                a.LastSyncedAt,
                a.LastSyncError,
                a.Users.OrderBy(u => u.Name).Select(u => u.Name).ToList(),
                a.Transactions.Count(t => !t.Splits.Any() && !t.Reviewed)));
}
