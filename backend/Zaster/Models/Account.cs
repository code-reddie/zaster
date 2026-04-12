using System.Collections.Generic;

namespace Zaster.Models;

public sealed record Account : Entity
{
    public required string Name { get; init; }

    public List<Transaction> Transactions { get; init; } = [];

    public List<User> Users { get; init; } = [];
}

public sealed record CreateAccount(string Name);
