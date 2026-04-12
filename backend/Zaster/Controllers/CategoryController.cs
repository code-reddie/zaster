using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Zaster.Database;
using Zaster.Models;

namespace Zaster.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoryController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    [HttpPost]
    public async Task<ActionResult<Category>> CreateCategory(
            CreateCategory dto,
            CancellationToken cancellationToken)
    {
        var category = new Category
        {
            Id = 0,
            Name = dto.Name,
            Icon = dto.Icon,
            Color = dto.Color,
            Description = dto.Description ?? string.Empty,
            ParentCategoryId = dto.ParentCategoryId
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Category>> GetCategory(
        int id,
        CancellationToken cancellationToken)
    {
        var category = await _context.Categories.FindAsync(id, cancellationToken);

        if (category == null)
        {
            return NotFound();
        }

        return category;
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteCategory(
        int id,
        CancellationToken cancellationToken)
    {
        var category = await _context.Categories.FindAsync(id, cancellationToken);

        if (category == null)
        {
            return NotFound();
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
