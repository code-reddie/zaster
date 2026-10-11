using System;
using System.Collections.Generic;

namespace Zaster.Accounts;

public sealed record AccountDto(
    int Id,
    string Name,
    string Iban,
    string? FinTsUserId,
    bool HasFinTsPin,
    IReadOnlyList<string> SyncTimes,
    DateTimeOffset? LastSyncedAt,
    string? LastSyncError,
    IReadOnlyList<string> Members,
    int OpenTransactions);

public sealed record CreateAccount(string Name, string Iban);

public sealed record UpdateAccount(string Name, string? FinTsUserId);
