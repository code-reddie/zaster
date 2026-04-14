namespace Zaster.Controllers.Authentication.Models;

public sealed record LoginRequest
{
    public required string Name { get; init; }

    public required string Password { get; init; }
}

