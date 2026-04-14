namespace Zaster.Controllers.Authentication.Models;

public sealed record RegisterRequest
{
    public required string Name { get; init; }

    public required string Password { get; init; }
}
