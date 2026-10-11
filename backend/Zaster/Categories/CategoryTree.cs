using System.Collections.Generic;
using System.Linq;

namespace Zaster.Categories;

internal static class CategoryTree
{
    /// <summary>
    /// Die Kategorie selbst und alle Kategorien darunter, egal wie tief.
    /// </summary>
    public static HashSet<int> WithDescendants(IEnumerable<Category> categories, int id)
    {
        var byParent = categories.ToLookup(c => c.ParentId);
        var result = new HashSet<int> { id };
        var pending = new Stack<int>([id]);
        while (pending.TryPop(out var current))
        {
            foreach (var child in byParent[current])
            {
                if (result.Add(child.Id))
                {
                    pending.Push(child.Id);
                }
            }
        }

        return result;
    }
}
