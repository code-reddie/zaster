using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Categorization;
using Zaster.Database;
using Zaster.Models;

namespace Zaster.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategorizationRuleController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    private int? GetUserId()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdString, out var userId) ? userId : null;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategorizationRuleDto>>> GetRules(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var rules = await _context.CategorizationRules
            .Where(r => r.Category!.UserId == userId)
            .OrderBy(r => r.Id)
            .Select(r => new CategorizationRuleDto(r.Id, r.Pattern, r.Field, r.CategoryId))
            .ToListAsync(cancellationToken);

        return Ok(rules);
    }

    [HttpPost]
    public async Task<ActionResult<CategorizationRuleDto>> CreateRule(
        CreateCategorizationRule dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(dto.Pattern))
        {
            return BadRequest("Suchbegriff darf nicht leer sein.");
        }

        if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId && c.UserId == userId, cancellationToken))
        {
            return BadRequest($"Category with ID {dto.CategoryId} does not exist.");
        }

        var rule = new CategorizationRule
        {
            Pattern = dto.Pattern.Trim(),
            Field = dto.Field,
            CategoryId = dto.CategoryId
        };

        _context.CategorizationRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new CategorizationRuleDto(rule.Id, rule.Pattern, rule.Field, rule.CategoryId));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRule(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var rule = await _context.CategorizationRules
            .FirstOrDefaultAsync(r => r.Id == id && r.Category!.UserId == userId, cancellationToken);
        if (rule == null)
        {
            return NotFound();
        }

        _context.CategorizationRules.Remove(rule);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Wendet die Regeln des angemeldeten Nutzers auf seine unkategorisierten Buchungen an.
    /// </summary>
    [HttpPost("apply")]
    public async Task<ActionResult<ApplyRulesResult>> ApplyRules(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var rules = await _context.CategorizationRules
            .Where(r => r.Category!.UserId == userId)
            .ToListAsync(cancellationToken);
        var transactions = await _context.Transactions
            .Where(t => t.CategoryId == null && t.Account!.Users.Any(u => u.Id == userId))
            .ToListAsync(cancellationToken);

        var updated = RuleEngine.Apply(transactions, rules);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new ApplyRulesResult(updated));
    }
}
