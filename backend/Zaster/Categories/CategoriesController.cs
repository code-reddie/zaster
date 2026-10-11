using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Common;
using Zaster.Database;

namespace Zaster.Categories;

[Route("api/accounts/{accountId:int}/categories")]
[AccountMember]
public sealed partial class CategoriesController(AppDbContext context) : ApiControllerBase
{
    /// <summary>
    /// Alle Kategorien des Kontos als flache Liste; den Baum ergeben <c>parentId</c> und <c>sortOrder</c>.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<CategoryDto>> GetCategories(int accountId, CancellationToken cancellationToken) =>
        await context.Categories
            .Where(c => c.AccountId == accountId)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Color, c.ParentId, c.SortOrder))
            .ToListAsync(cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(int accountId, SaveCategory request, CancellationToken cancellationToken)
    {
        var categories = await LoadAsync(accountId, cancellationToken);
        if (Validate(request, categories, null) is { } error)
        {
            return BadRequestProblem(error);
        }

        var category = new Category
        {
            AccountId = accountId,
            Name = request.Name.Trim(),
            Color = NormalizeColor(request.Color),
            ParentId = request.ParentId,
            SortOrder = NextSortOrder(categories, request.ParentId),
        };
        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);

        return CategoryDto.From(category);
    }

    /// <summary>
    /// Benennt um, färbt um oder hängt die Kategorie samt Unterkategorien woanders in den Baum.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(int accountId, int id, SaveCategory request, CancellationToken cancellationToken)
    {
        var categories = await LoadAsync(accountId, cancellationToken);
        var category = categories.FirstOrDefault(c => c.Id == id);
        if (category is null)
        {
            return NotFoundProblem("Die Kategorie gibt es nicht.");
        }

        if (Validate(request, categories, category) is { } error)
        {
            return BadRequestProblem(error);
        }

        if (category.ParentId != request.ParentId)
        {
            category.ParentId = request.ParentId;
            category.SortOrder = NextSortOrder(categories, request.ParentId);
        }

        category.Name = request.Name.Trim();
        category.Color = NormalizeColor(request.Color);
        await context.SaveChangesAsync(cancellationToken);

        return CategoryDto.From(category);
    }

    [HttpPut("order")]
    public async Task<IActionResult> Reorder(int accountId, ReorderCategories request, CancellationToken cancellationToken)
    {
        var siblings = await context.Categories
            .Where(c => c.AccountId == accountId && c.ParentId == request.ParentId)
            .ToListAsync(cancellationToken);
        if (!siblings.Select(c => c.Id).Order().SequenceEqual(request.Ids.Order()))
        {
            return BadRequestProblem("Die Liste passt nicht zu den Kategorien auf dieser Ebene.");
        }

        foreach (var category in siblings)
        {
            category.SortOrder = IndexOf(request.Ids, category.Id);
        }

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Löscht die Kategorie mit allen Unterkategorien und deren Regeln. Ihre Buchungen sind danach
    /// ohne Kategorie und wieder offen, damit sie auf der Seite „Ohne Kategorie“ auftauchen.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCategory(int accountId, int id, CancellationToken cancellationToken)
    {
        var categories = await LoadAsync(accountId, cancellationToken);
        if (categories.All(c => c.Id != id))
        {
            return NotFoundProblem("Die Kategorie gibt es nicht.");
        }

        var ids = CategoryTree.WithDescendants(categories, id);
        // Aufgeteilte Buchungen verlieren alle Teile, nicht nur den gelöschten: eine halbe Aufteilung
        // ergäbe nicht mehr den Buchungsbetrag.
        await using var dbTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var affected = await context.Transactions
            .Where(t => t.AccountId == accountId && t.Splits.Any(s => ids.Contains(s.CategoryId)))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        await context.TransactionSplits
            .Where(s => affected.Contains(s.TransactionId))
            .ExecuteDeleteAsync(cancellationToken);
        await context.Transactions
            .Where(t => affected.Contains(t.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Reviewed, false), cancellationToken);
        // Unterkategorien und Regeln löscht die Datenbank mit (ON DELETE CASCADE).
        await context.Categories.Where(c => c.Id == id).ExecuteDeleteAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return NoContent();
    }

    private Task<List<Category>> LoadAsync(int accountId, CancellationToken cancellationToken) =>
        context.Categories.Where(c => c.AccountId == accountId).ToListAsync(cancellationToken);

    private static string? Validate(SaveCategory request, List<Category> categories, Category? category)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Bitte gib der Kategorie einen Namen.";
        }

        if (!string.IsNullOrWhiteSpace(request.Color) && NormalizeColor(request.Color) is null)
        {
            return "Die Farbe muss im Format #rrggbb angegeben werden.";
        }

        if (request.ParentId is { } parentId)
        {
            if (categories.All(c => c.Id != parentId))
            {
                return "Die übergeordnete Kategorie gibt es in diesem Konto nicht.";
            }

            if (category is not null && CategoryTree.WithDescendants(categories, category.Id).Contains(parentId))
            {
                return "Eine Kategorie kann nicht unter sich selbst oder ihre Unterkategorien verschoben werden.";
            }
        }

        var duplicate = categories.Any(c =>
            c.ParentId == request.ParentId
            && c.Id != category?.Id
            && string.Equals(c.Name, request.Name.Trim(), System.StringComparison.OrdinalIgnoreCase));
        return duplicate ? "Auf dieser Ebene gibt es schon eine Kategorie mit diesem Namen." : null;
    }

    private static int NextSortOrder(List<Category> categories, int? parentId) =>
        categories.Where(c => c.ParentId == parentId).Select(c => c.SortOrder + 1).DefaultIfEmpty(0).Max();

    private static int IndexOf(IReadOnlyList<int> ids, int id)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (ids[i] == id)
            {
                return i;
            }
        }

        return ids.Count;
    }

    private static string? NormalizeColor(string? color) =>
        string.IsNullOrWhiteSpace(color) ? null
        : ColorRegex().IsMatch(color.Trim()) ? color.Trim().ToLowerInvariant()
        : null;

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorRegex();
}
