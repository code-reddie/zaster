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
    IReadOnlyList<AccountMemberDto> Members,
    int OpenTransactions);

public sealed record AccountMemberDto(int Id, string Name);

public sealed record CreateAccount(string Name, string Iban);

/// <param name="Ids">Alle eigenen Konten in der gewünschten Reihenfolge.</param>
public sealed record ReorderAccounts(List<int> Ids);

public sealed record UpdateAccount(string Name, string? FinTsUserId);

/// <param name="Name">Anmeldename eines schon registrierten Nutzers.</param>
public sealed record AddMember(string? Name);
