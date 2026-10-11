using System;
using System.Text.Json.Serialization;
using Zaster.Accounts;
using Zaster.Categories;
using Zaster.Database;

namespace Zaster.Transactions;

[JsonConverter(typeof(JsonStringEnumConverter<TransactionSource>))]
public enum TransactionSource
{
    /// <summary>
    /// Angelegt vor dem Refactoring, als die Herkunft noch nicht gespeichert wurde.
    /// </summary>
    Unbekannt,
    Manuell,
    Csv,
    FinTs
}

public sealed record Transaction : Entity
{
    public int AccountId { get; init; }

    public Account? Account { get; init; }

    /// <summary>
    /// Buchungstag. Als Datum ohne Uhrzeit gespeichert (<c>yyyy-MM-dd</c>), damit SQLite danach sortieren und filtern kann.
    /// </summary>
    public required DateOnly Buchung { get; init; }

    public required DateOnly Valuta { get; init; }

    public required string Auftraggeber { get; init; }

    public required string Buchungstext { get; init; }

    public string Verwendungszweck { get; init; } = string.Empty;

    public required decimal Betrag { get; init; }

    public int? CategoryId { get; set; }

    public Category? Category { get; init; }

    /// <summary>
    /// Vom Nutzer abgehakt („geprüft“). Eine Buchung mit Kategorie gilt immer als geprüft.
    /// </summary>
    public bool Reviewed { get; set; }

    public TransactionSource Source { get; init; }

    /// <summary>
    /// Wann Zaster die Buchung angelegt hat (UTC), z. B. um nach einem Abruf neue Buchungen zu erkennen.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Setzt die Kategorie. Eine Kategorie macht die Buchung automatisch geprüft;
    /// ohne Kategorie bleibt sie offen, bis jemand sie abhakt.
    /// </summary>
    public void Categorize(int? categoryId)
    {
        CategoryId = categoryId;
        Reviewed = categoryId is not null;
    }
}
