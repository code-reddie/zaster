using System.Collections.Generic;

namespace Zaster.Categories;

public sealed record CategoryDto(int Id, string Name, string? Color, int? ParentId, int SortOrder)
{
    public static CategoryDto From(Category c) => new(c.Id, c.Name, c.Color, c.ParentId, c.SortOrder);
}

public sealed record SaveCategory(string Name, string? Color, int? ParentId);

/// <summary>
/// Neue Reihenfolge der Kinder einer Elternkategorie (null = oberste Ebene).
/// </summary>
public sealed record ReorderCategories(int? ParentId, IReadOnlyList<int> Ids);

public sealed record RuleDto(int Id, string Pattern, RuleField Field, int CategoryId, int SortOrder)
{
    public static RuleDto From(Rule r) => new(r.Id, r.Pattern, r.Field, r.CategoryId, r.SortOrder);
}

public sealed record SaveRule(string Pattern, RuleField Field, int CategoryId);

public sealed record ReorderRules(IReadOnlyList<int> Ids);

public sealed record ApplyRulesResult(int Updated);
