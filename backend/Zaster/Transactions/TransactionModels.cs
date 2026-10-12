using System;
using System.Collections.Generic;
using System.Linq;

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
    decimal? Saldo,
    string? Bemerkung,
    IReadOnlyList<TransactionSplitDto> Splits,
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
        t.Saldo,
        t.Bemerkung,
        t.Splits.OrderBy(s => s.Betrag < 0 ? s.Betrag : -s.Betrag).ThenBy(s => s.Id).Select(s => new TransactionSplitDto(s.CategoryId, s.Betrag)).ToList(),
        t.Reviewed,
        t.Source);
}

/// <summary>Teilbetrag einer Kategorie, mit dem Vorzeichen der Buchung.</summary>
public sealed record TransactionSplitDto(int CategoryId, decimal Betrag);

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
    decimal Betrag,
    string? Bemerkung = null);

/// <summary>
/// Ohne Valuta gilt der Buchungstag, wie bei Buchungen von Hand. <paramref name="Saldo"/> ist der
/// Kontostand nach der Buchung, soweit die Quelle ihn kennt.
/// </summary>
public sealed record ImportTransaction(
    DateOnly Buchung,
    DateOnly? Valuta,
    string Auftraggeber,
    string Buchungstext,
    string? Verwendungszweck,
    decimal Betrag,
    decimal? Saldo = null);

public sealed record ImportTransactions(IReadOnlyList<ImportTransaction> Transactions);

public sealed record ImportResult(int Imported, int Skipped);

/// <summary>
/// Ergebnis der Lückenprüfung über die Kontostände. <paramref name="Kontostand"/> ist der letzte bekannte
/// Stand laut Bank (am Tag <paramref name="Stand"/>), <paramref name="Geprueft"/> die Zahl der Buchungen mit
/// Kontostand, <paramref name="OhneKontostand"/> die Zahl der Buchungen von Bank oder CSV ohne Kontostand.
/// </summary>
public sealed record BalanceCheckDto(
    decimal? Kontostand,
    DateOnly? Stand,
    int Geprueft,
    int OhneKontostand,
    IReadOnlyList<BalanceGap> Luecken);

/// <summary>
/// Eine Stelle, an der die Kontostände nicht zusammenpassen. <paramref name="Von"/> ist der letzte Tag
/// davor mit stimmigem Kontostand (null, wenn es keinen gibt), <paramref name="Bis"/> der Tag, an dem es
/// auffällt. <paramref name="Differenz"/> ist der Betrag, der fehlt (positiv: fehlende Einnahmen), oder null,
/// wenn die Kontostände innerhalb eines Tages nicht zusammenpassen und sich nicht beziffern lassen.
/// </summary>
public sealed record BalanceGap(DateOnly? Von, DateOnly Bis, decimal? Differenz);

public sealed record SetCategory(int? CategoryId);

/// <summary>
/// Teilt die Buchung auf mehrere Kategorien auf. Die Teilbeträge müssen zusammen den Buchungsbetrag ergeben;
/// eine leere Liste entfernt alle Kategorien.
/// </summary>
public sealed record SetSplits(IReadOnlyList<TransactionSplitDto> Splits);

public sealed record SetReviewed(bool Reviewed);

/// <summary>Eigene Bemerkung zur Buchung; leer oder null entfernt sie.</summary>
public sealed record SetBemerkung(string? Bemerkung);

/// <summary>
/// Hakt die angegebenen Buchungen ab, ohne Ids alle offenen des Kontos.
/// </summary>
public sealed record ReviewTransactions(IReadOnlyList<int>? Ids);

public sealed record ReviewResult(int Updated);
