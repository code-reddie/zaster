using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zaster.Common;
using Zaster.Database;
using Zaster.Users;

namespace Zaster.Auth;

[Route("api/auth")]
[AllowAnonymous]
public sealed partial class AuthController(ILogger<AuthController> logger, AppDbContext context, AuthService authService) : ApiControllerBase
{
    private const int MinPasswordLength = 8;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return BadRequestProblem("Bitte gib einen Namen ein.");
        }

        if ((request.Password?.Length ?? 0) < MinPasswordLength)
        {
            return BadRequestProblem($"Das Passwort muss mindestens {MinPasswordLength} Zeichen lang sein.");
        }

        if (await context.Users.AnyAsync(u => u.Name == name, cancellationToken))
        {
            LogUserAlreadyExists(logger, name);
            return ConflictProblem("Diesen Namen gibt es schon.");
        }

        var user = new User { Name = name, PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password) };
        context.Users.Add(user);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            LogUserAlreadyExists(logger, name);
            return ConflictProblem("Diesen Namen gibt es schon.");
        }

        return new AuthResponse(user.Id, user.Name, authService.CreateToken(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var user = await context.Users.FirstOrDefaultAsync(u => u.Name == name, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password ?? string.Empty, user.PasswordHash))
        {
            LogLoginFailed(logger, name);
            return Problem(detail: "Name oder Passwort stimmen nicht.", statusCode: 401);
        }

        return new AuthResponse(user.Id, user.Name, authService.CreateToken(user));
    }
}
