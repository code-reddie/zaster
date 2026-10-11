using System.Linq;
using Zaster.Categories;

namespace Zaster.Tests;

public sealed class CategoryTreeTests
{
    private static Category Kategorie(int id, int? parentId) => new() { Id = id, Name = $"K{id}", ParentId = parentId };

    [Fact]
    public void WithDescendants_findet_alle_Ebenen()
    {
        // Autos (1) → Passat (2) → Reifen (4); Autos → Corsa (3); Lebensmittel (5)
        var categories = new[] { Kategorie(1, null), Kategorie(2, 1), Kategorie(3, 1), Kategorie(4, 2), Kategorie(5, null) };

        Assert.Equal([1, 2, 3, 4], CategoryTree.WithDescendants(categories, 1).Order().ToArray());
        Assert.Equal([2, 4], CategoryTree.WithDescendants(categories, 2).Order().ToArray());
        Assert.Equal([5], CategoryTree.WithDescendants(categories, 5).ToArray());
    }
}
