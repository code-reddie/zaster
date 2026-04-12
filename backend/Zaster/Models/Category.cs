using System.Collections.Generic;

namespace Zaster.Models;

public sealed record Category : Entity
{
    public required string Name { get; init; }

    public string? Icon { get; init; }

    public string? Color { get; init; }

    public string Description { get; init; } = string.Empty;

    public int? ParentCategoryId { get; init; }

    public Category? ParentCategory { get; init; }

    public List<Category> Subcategories { get; init; } = [];

    public List<Transaction> Transactions { get; init; } = [];
}

public sealed record CreateCategory(
    string Name,
    string? Icon,
    string? Color,
    string? Description,
    int? ParentCategoryId);