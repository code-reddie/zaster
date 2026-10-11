namespace Zaster.Auth;

public sealed record LoginRequest(string Name, string Password);

public sealed record RegisterRequest(string Name, string Password);

public sealed record AuthResponse(int UserId, string UserName, string Token);
