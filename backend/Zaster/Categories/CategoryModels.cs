namespace Zaster.Categories;

public sealed record CategoryDto(int Id, string Name, string? Color, int? ParentId)
{
    public static CategoryDto From(Category c) => new(c.Id, c.Name, c.Color, c.ParentId);
}

public sealed record SaveCategory(string Name, string? Color, int? ParentId);

public sealed record RuleDto(int Id, string Pattern, RuleField Field, int CategoryId)
{
    public static RuleDto From(Rule r) => new(r.Id, r.Pattern, r.Field, r.CategoryId);
}

public sealed record SaveRule(string Pattern, RuleField Field, int CategoryId);

public sealed record ApplyRulesResult(int Updated);
