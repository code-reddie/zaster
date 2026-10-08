using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Database;
using Zaster.Models;

namespace Zaster.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoryController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    private static CategoryDto ToDto(Category c) =>
        new(c.Id, c.Name, c.Icon, c.Color, c.Description, c.ParentCategoryId);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Icon, c.Color, c.Description, c.ParentCategoryId))
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
            CreateCategory dto,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest("Name darf nicht leer sein.");
        }

        var category = new Category
        {
            Id = 0,
            Name = dto.Name.Trim(),
            Icon = dto.Icon,
            Color = dto.Color,
            Description = dto.Description ?? string.Empty,
            ParentCategoryId = dto.ParentCategoryId
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, ToDto(category));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(
        int id,
        CancellationToken cancellationToken)
    {
        var category = await _context.Categories.FindAsync([id], cancellationToken);

        if (category == null)
        {
            return NotFound();
        }

        return ToDto(category);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteCategory(
        int id,
        CancellationToken cancellationToken)
    {
        var category = await _context.Categories.FindAsync([id], cancellationToken);

        if (category == null)
        {
            return NotFound();
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
