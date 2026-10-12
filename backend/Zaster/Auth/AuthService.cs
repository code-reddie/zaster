using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Zaster.Users;

namespace Zaster.Auth;

public sealed class AuthService(IConfiguration config, TimeProvider timeProvider)
{
    /// <summary>
    /// So lange gilt ein Token. Das Frontend holt sich beim Benutzen täglich einen neuen (<c>POST /api/auth/refresh</c>),
    /// neu anmelden muss man sich also erst nach dieser Zeit ohne Nutzung.
    /// </summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(7);

    private readonly IConfiguration _config = config;

    public string CreateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        var keyString = _config["JwtSettings:Key"] ?? throw new InvalidOperationException("JWT key is not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now + TokenLifetime,
            SigningCredentials = creds,
            Issuer = _config["JwtSettings:Issuer"],
            Audience = _config["JwtSettings:Audience"]
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
