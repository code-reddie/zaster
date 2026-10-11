using System;
using System.Collections.Generic;

namespace Zaster.FinTs;

public sealed record FinTsFetchRequest(
    string Iban,
    string? UserId,
    string Pin,
    int? Days);

public sealed record FinTsBankMessage(string Code, string Message);

public sealed record FinTsTransaction(
    DateOnly Buchung,
    DateOnly Valuta,
    string Auftraggeber,
    string Buchungstext,
    string Verwendungszweck,
    decimal Betrag);

public sealed record FinTsFetchResult(
    bool Success,
    string? Error,
    IReadOnlyList<FinTsBankMessage> Messages,
    IReadOnlyList<FinTsTransaction> Transactions,
    IReadOnlyList<string> Diagnostics,
    bool LoginRejected = false);

public sealed record UpdateSyncTimes(List<string> Times);

public sealed record FinTsSyncRequest(string? UserId, string? Pin, bool SavePin);

public sealed record FinTsSyncResult(
    bool Success,
    string? Error,
    IReadOnlyList<FinTsBankMessage> Messages,
    IReadOnlyList<string> Diagnostics,
    int Imported,
    int Skipped);
