using System;
using System.Collections.Generic;

namespace Zaster.FinTs;

public sealed record FinTsTestRequest(
    string Iban,
    string? UserId,
    string Pin,
    int? Days);

public sealed record FinTsBankMessage(string Code, string Message);

public sealed record FinTsTransactionPreview(
    DateTimeOffset Buchung,
    DateTimeOffset Valuta,
    string Auftragsgeber,
    string Buchungstext,
    string Verwendungszweck,
    decimal Betrag);

public sealed record FinTsTestResult(
    bool Success,
    string? Error,
    IReadOnlyList<FinTsBankMessage> Messages,
    IReadOnlyList<FinTsTransactionPreview> Transactions,
    IReadOnlyList<string> Diagnostics,
    bool LoginRejected = false);

public sealed record FinTsSyncRequest(string? UserId, string? Pin, bool SavePin);

public sealed record FinTsSyncResult(
    bool Success,
    string? Error,
    IReadOnlyList<FinTsBankMessage> Messages,
    IReadOnlyList<string> Diagnostics,
    int Imported,
    int Skipped,
    IReadOnlyList<Models.TransactionDto> Transactions,
    Models.AccountDto Account);
