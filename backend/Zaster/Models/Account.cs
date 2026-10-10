using System;
using System.Collections.Generic;

namespace Zaster.Models;

public sealed record Account : Entity
{
    public required string Name { get; init; }

    public required string Iban { get; init; }

    /// <summary>
    /// FinTS-Benutzerkennung, falls sie nicht aus der IBAN abgeleitet werden soll.
    /// </summary>
    public string? FinTsUserId { get; set; }

    /// <summary>
    /// Mit ASP.NET Data Protection verschlüsselte PIN. Verlässt das Backend nie.
    /// </summary>
    public string? FinTsPin { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public List<Transaction> Transactions { get; init; } = [];

    public List<User> Users { get; init; } = [];
}

public sealed record AccountDto(
    int Id,
    string Name,
    string Iban,
    string? FinTsUserId,
    bool HasFinTsPin,
    DateTimeOffset? LastSyncedAt)
{
    public static AccountDto From(Account account) => new(
        account.Id,
        account.Name,
        account.Iban,
        account.FinTsUserId,
        account.FinTsPin is not null,
        account.LastSyncedAt);
}

public sealed record CreateAccount(string Name, string Iban);