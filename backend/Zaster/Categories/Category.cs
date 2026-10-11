using System.Collections.Generic;
using Zaster.Accounts;
using Zaster.Database;
using Zaster.Transactions;

namespace Zaster.Categories;

/// <summary>
/// Kategorie eines Kontos. Kategorien bilden einen Baum mit beliebig vielen Ebenen.
/// </summary>
public sealed record Category : Entity
{
    public int AccountId { get; init; }

    public Account? Account { get; init; }

    public required string Name { get; set; }

    /// <summary>
    /// Farbe als <c>#rrggbb</c>, null = Farbe der Elternkategorie bzw. neutral.
    /// </summary>
    public string? Color { get; set; }

    public int? ParentId { get; set; }

    public Category? Parent { get; init; }

    public List<Category> Children { get; init; } = [];

    /// <summary>
    /// Reihenfolge unter Geschwistern, kleinere zuerst.
    /// </summary>
    public int SortOrder { get; set; }

    public List<TransactionSplit> Splits { get; init; } = [];
}
