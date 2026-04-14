using System.Collections.Generic;

namespace Zaster.Models;

public sealed record User : Entity
{
    public required string Name { get; init; }

    public required string PasswordHash { get; init; }

    public List<Account> Accounts { get; init; } = [];
}