using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Database;
using Zaster.Models;

namespace Zaster.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    [HttpPost]
    public async Task<ActionResult<User>> CreateUser(
    CreateUser dto,
    CancellationToken cancellationToken)
    {
        var user = new User
        {
            Id = 0,
            Name = dto.Name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(
        int id,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync(id, cancellationToken);

        if (user == null)
        {
            return NotFound();
        }

        return user;
    }

    [HttpPost("login")]
    public async Task<ActionResult<User>> Login(
    LoginRequest dto,
    CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Name == dto.Name, cancellationToken);

        if (user == null)
        {
            return Unauthorized();
        }

        bool isCorrect = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);

        if (!isCorrect)
        {
            return Unauthorized();
        }

        return Ok(user);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(
        int id,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync(id, cancellationToken);

        if (user == null)
        {
            return NotFound();
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
