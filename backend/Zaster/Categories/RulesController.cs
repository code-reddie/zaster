using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Common;
using Zaster.Database;
using Zaster.Transactions;

namespace Zaster.Categories;

[Route("api/accounts/{accountId:int}/rules")]
[AccountMember]
public sealed class RulesController(AppDbContext context) : ApiControllerBase
{
    /// <summary>
    /// Regeln in der Reihenfolge, in der sie geprüft werden.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<RuleDto>> GetRules(int accountId, CancellationToken cancellationToken) =>
        (await context.Rules
            .Include(r => r.Conditions)
            .Where(r => r.AccountId == accountId)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken))
            .Select(RuleDto.From)
            .ToList();

    /// <summary>
    /// Legt eine Regel am Ende der Reihenfolge an.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RuleDto>> CreateRule(int accountId, SaveRule request, CancellationToken cancellationToken)
    {
        if (await ValidateAsync(accountId, request, cancellationToken) is { } error)
        {
            return BadRequestProblem(error);
        }

        var sortOrder = await context.Rules
            .Where(r => r.AccountId == accountId)
            .Select(r => (int?)r.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        var rule = new Rule
        {
            AccountId = accountId,
            Conditions = ToConditions(request),
            Direction = request.Direction,
            CategoryId = request.CategoryId,
            SortOrder = sortOrder + 1,
        };
        context.Rules.Add(rule);
        await context.SaveChangesAsync(cancellationToken);

        return RuleDto.From(rule);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RuleDto>> UpdateRule(int accountId, int id, SaveRule request, CancellationToken cancellationToken)
    {
        var rule = await context.Rules
            .Include(r => r.Conditions)
            .FirstOrDefaultAsync(r => r.Id == id && r.AccountId == accountId, cancellationToken);
        if (rule is null)
        {
            return NotFoundProblem("Die Regel gibt es nicht.");
        }

        if (await ValidateAsync(accountId, request, cancellationToken) is { } error)
        {
            return BadRequestProblem(error);
        }

        rule.Conditions.Clear();
        rule.Conditions.AddRange(ToConditions(request));
        rule.Direction = request.Direction;
        rule.CategoryId = request.CategoryId;
        await context.SaveChangesAsync(cancellationToken);

        return RuleDto.From(rule);
    }

    [HttpPut("order")]
    public async Task<IActionResult> Reorder(int accountId, ReorderRules request, CancellationToken cancellationToken)
    {
        var rules = await context.Rules.Where(r => r.AccountId == accountId).ToListAsync(cancellationToken);
        if (!rules.Select(r => r.Id).Order().SequenceEqual(request.Ids.Order()))
        {
            return BadRequestProblem("Die Liste passt nicht zu den Regeln des Kontos.");
        }

        var positions = request.Ids.Select((ruleId, index) => (ruleId, index)).ToDictionary(p => p.ruleId, p => p.index);
        foreach (var rule in rules)
        {
            rule.SortOrder = positions[rule.Id];
        }

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRule(int accountId, int id, CancellationToken cancellationToken)
    {
        var deleted = await context.Rules.Where(r => r.Id == id && r.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? NotFoundProblem("Die Regel gibt es nicht.") : NoContent();
    }

    /// <summary>
    /// Wendet die Regeln auf alle Buchungen des Kontos ohne Kategorie an, auch auf alte und schon abgehakte.
    /// </summary>
    [HttpPost("apply")]
    public async Task<ApplyRulesResult> ApplyRules(int accountId, CancellationToken cancellationToken)
    {
        var rules = await context.Rules.Include(r => r.Conditions).Where(r => r.AccountId == accountId).ToListAsync(cancellationToken);
        var transactions = await context.Transactions
            .Where(t => t.AccountId == accountId && !t.Splits.Any())
            .ToListAsync(cancellationToken);

        var before = transactions.ToDictionary(t => t.Id, TransactionState.From);
        var updated = RuleEngine.Apply(transactions, rules);
        await context.SaveChangesAsync(cancellationToken);

        var previous = transactions.Where(t => t.IsCategorized).Select(t => before[t.Id]).ToList();
        return new ApplyRulesResult(updated, previous);
    }

    private const int MaxConditions = 10;

    private static List<RuleCondition> ToConditions(SaveRule request) =>
        request.Conditions.Select(c => new RuleCondition { Field = c.Field, Pattern = c.Pattern.Trim() }).ToList();

    private async Task<string?> ValidateAsync(int accountId, SaveRule request, CancellationToken cancellationToken)
    {
        if (request.Conditions is not { Count: > 0 })
        {
            return "Eine Regel braucht mindestens eine Bedingung.";
        }

        if (request.Conditions.Count > MaxConditions)
        {
            return $"Eine Regel hat höchstens {MaxConditions} Bedingungen.";
        }

        if (request.Conditions.Any(c => string.IsNullOrWhiteSpace(c.Pattern)))
        {
            return "Bitte gib in jeder Bedingung einen Suchbegriff ein.";
        }

        var categoryExists = await context.Categories.AnyAsync(c => c.Id == request.CategoryId && c.AccountId == accountId, cancellationToken);
        return categoryExists ? null : "Die Kategorie gibt es in diesem Konto nicht.";
    }
}
