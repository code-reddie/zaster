namespace Zaster.Controllers.Authentication.Models;

public sealed record AuthResponse
{
    public required int UserId { get; init; }

    public required string UserName { get; init; }

    public required string Token { get; init; }
}
