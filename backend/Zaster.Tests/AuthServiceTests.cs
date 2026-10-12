using System;
using System.Collections.Generic;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Zaster.Auth;
using Zaster.Users;

namespace Zaster.Tests;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Token_GiltSiebenTageAbAusstellung()
    {
        var token = Read(CreateToken(Now));

        Assert.Equal(Now.UtcDateTime, token.IssuedAt);
        Assert.Equal(Now.UtcDateTime.AddDays(7), token.ValidTo);
        Assert.Equal("42", token.Claims.Single(c => c.Type == "nameid").Value);
    }

    [Fact]
    public void NeuerToken_VerschiebtAblaufNachHinten()
    {
        var first = Read(CreateToken(Now));
        var second = Read(CreateToken(Now.AddDays(3)));

        Assert.Equal(first.ValidTo.AddDays(3), second.ValidTo);
    }

    private static string CreateToken(DateTimeOffset now)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Key"] = new string('k', 64),
                ["JwtSettings:Issuer"] = "zaster",
                ["JwtSettings:Audience"] = "zaster",
            })
            .Build();
        return new AuthService(config, new FixedTime(now)).CreateToken(new User { Id = 42, Name = "Anna", PasswordHash = "x" });
    }

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
