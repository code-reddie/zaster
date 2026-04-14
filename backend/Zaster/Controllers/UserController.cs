using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Zaster.Database;
using Zaster.Models;

namespace Zaster.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

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
