using System.Collections.Generic;

namespace Zaster.Categories;

public sealed record CategoryDto(int Id, string Name, string? Color, int? ParentId)
{
    public static CategoryDto From(Category c) => new(c.Id, c.Name, c.Color, c.ParentId);
}

public sealed record SaveCategory(string Name, string? Color, int? ParentId);

public sealed record RuleDto(int Id, string Pattern, RuleField Field, RuleDirection Direction, int CategoryId, int SortOrder)
{
    public static RuleDto From(Rule r) => new(r.Id, r.Pattern, r.Field, r.Direction, r.CategoryId, r.SortOrder);
}

public sealed record SaveRule(string Pattern, RuleField Field, int CategoryId, RuleDirection Direction = RuleDirection.Beide);

public sealed record ReorderRules(IReadOnlyList<int> Ids);

public sealed record ApplyRulesResult(int Updated);
