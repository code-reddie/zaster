using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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

    private int? GetUserId()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdString, out var userId) ? userId : null;
    }

    private static CategoryDto ToDto(Category category) => new(
        category.Id,
        category.Name,
        category.Icon,
        category.Color,
        category.Description,
        category.ParentCategoryId);

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
            CreateCategory dto,
            CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (dto.ParentCategoryId is not null)
        {
            var parentExists = await _context.Categories
                .AnyAsync(c => c.Id == dto.ParentCategoryId && c.UserId == userId, cancellationToken);
            if (!parentExists)
            {
                return BadRequest($"Category with ID {dto.ParentCategoryId} does not exist.");
            }
        }

        var category = new Category
        {
            Id = 0,
            Name = dto.Name,
            Icon = dto.Icon,
            Color = dto.Color,
            Description = dto.Description ?? string.Empty,
            ParentCategoryId = dto.ParentCategoryId,
            UserId = userId.Value
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, ToDto(category));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var categories = await _context.Categories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return Ok(categories.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(ToDto(category));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteCategory(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);

        if (category == null)
        {
            return NotFound();
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
