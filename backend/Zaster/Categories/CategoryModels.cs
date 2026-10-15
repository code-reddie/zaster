using System.Collections.Generic;
using System.Linq;
using Zaster.Transactions;

namespace Zaster.Categories;

public sealed record CategoryDto(int Id, string Name, string? Color, int? ParentId)
{
    public static CategoryDto From(Category c) => new(c.Id, c.Name, c.Color, c.ParentId);
}

public sealed record SaveCategory(string Name, string? Color, int? ParentId);

public sealed record RuleConditionDto(RuleField Field, string Pattern);

public sealed record RuleDto(int Id, IReadOnlyList<RuleConditionDto> Conditions, RuleDirection Direction, int CategoryId, int SortOrder)
{
    public static RuleDto From(Rule r) => new(
        r.Id,
        r.Conditions.OrderBy(c => c.Id).Select(c => new RuleConditionDto(c.Field, c.Pattern)).ToList(),
        r.Direction,
        r.CategoryId,
        r.SortOrder);
}

public sealed record SaveRule(IReadOnlyList<RuleConditionDto> Conditions, int CategoryId, RuleDirection Direction = RuleDirection.Beide);

public sealed record ReorderRules(IReadOnlyList<int> Ids);

/// <summary>
/// <paramref name="Previous"/> ist der Zustand der zugeordneten Buchungen vorher, zum Rückgängigmachen.
/// </summary>
public sealed record ApplyRulesResult(int Updated, IReadOnlyList<TransactionState> Previous);
