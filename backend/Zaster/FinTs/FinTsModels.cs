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
    IReadOnlyList<FinTsTransactionPreview> Transactions);
