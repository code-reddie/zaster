using System;
using System.Collections.Generic;

namespace Zaster.Transactions;

public sealed record TransactionDto(
    int Id,
    int AccountId,
    DateOnly Buchung,
    DateOnly Valuta,
    string Auftraggeber,
    string Buchungstext,
    string Verwendungszweck,
    decimal Betrag,
    int? CategoryId,
    bool Reviewed,
    TransactionSource Source)
{
    public static TransactionDto From(Transaction t) => new(
        t.Id,
        t.AccountId,
        t.Buchung,
        t.Valuta,
        t.Auftraggeber,
        t.Buchungstext,
        t.Verwendungszweck,
        t.Betrag,
        t.CategoryId,
        t.Reviewed,
        t.Source);
}

public sealed record TransactionPage(IReadOnlyList<TransactionDto> Items, int Total, decimal Einnahmen, decimal Ausgaben);

public sealed record TransactionQuery(
    DateOnly? From,
    DateOnly? To,
    string? Search,
    int? CategoryId,
    bool WithoutCategory = false,
    bool Open = false,
    int Offset = 0,
    int Limit = 100);

public sealed record CreateTransaction(
    DateOnly Buchung,
    DateOnly? Valuta,
    string Auftraggeber,
    string? Buchungstext,
    string? Verwendungszweck,
    decimal Betrag);

/// <summary>Ohne Valuta gilt der Buchungstag, wie bei Buchungen von Hand.</summary>
public sealed record ImportTransaction(
    DateOnly Buchung,
    DateOnly? Valuta,
    string Auftraggeber,
    string Buchungstext,
    string? Verwendungszweck,
    decimal Betrag);

public sealed record ImportTransactions(IReadOnlyList<ImportTransaction> Transactions);

public sealed record ImportResult(int Imported, int Skipped);

public sealed record SetCategory(int? CategoryId);

public sealed record SetReviewed(bool Reviewed);

/// <summary>
/// Hakt die angegebenen Buchungen ab, ohne Ids alle offenen des Kontos.
/// </summary>
public sealed record ReviewTransactions(IReadOnlyList<int>? Ids);

public sealed record ReviewResult(int Updated);
