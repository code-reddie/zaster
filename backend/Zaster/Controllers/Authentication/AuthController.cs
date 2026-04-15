using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zaster.Authentication;
using Zaster.Controllers.Authentication.Models;
using Zaster.Database;
using Zaster.Models;

namespace Zaster.Controllers.Authentication;

[ApiController]
[Route("api/[controller]")]
public partial class AuthController(ILogger<AuthController> logger, AppDbContext context, AuthService authService) : ControllerBase
{
    private readonly ILogger<AuthController> _logger = logger;
    private readonly AppDbContext _context = context;
    private readonly AuthService _authService = authService;

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Name == request.Name, cancellationToken);

        if (existingUser != null)
        {
            LogUserAlreadyExists(_logger, request.Name);

            return Conflict(new
            {
                code = "USER_ALREADY_EXISTS",
                message = "A user with this name is already registered."
            });
        }

        var user = new User
        {
            Id = 0,
            Name = request.Name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        string token;
        try
        {
            token = _authService.CreateToken(user);
        }
        catch
        {
            LogTokenCreationFailed(_logger, user.Name);

            return StatusCode(500, new
            {
                code = "TOKEN_CREATION_FAILED",
                message = "An error occurred while creating the authentication token.",
            });
        }

        return Ok(new AuthResponse
        {
            UserId = user.Id,
            UserName = user.Name,
            Token = token
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Name == request.Name, cancellationToken);

        if (user == null)
        {
            LogUserNotFound(_logger, request.Name);

            return Unauthorized();
        }

        bool isCorrect = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!isCorrect)
        {
            LogInvalidPassword(_logger, request.Name);

            return Unauthorized();
        }

        string token;
        try
        {
            token = _authService.CreateToken(user);
        }
        catch
        {
            LogTokenCreationFailed(_logger, user.Name);

            return StatusCode(500, new
            {
                code = "TOKEN_CREATION_FAILED",
                message = "An error occurred while creating the authentication token.",
            });
        }

        return Ok(new AuthResponse
        {
            UserId = user.Id,
            UserName = user.Name,
            Token = token
        });
    }
}
