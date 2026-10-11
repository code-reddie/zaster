using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    /// <summary>
    /// Kategorien der Buchung mit Teilbetrag. Leer = ohne Kategorie, sonst ergeben die Teilbeträge
    /// zusammen genau <see cref="Betrag"/>. Der Normalfall ist ein Teil über den ganzen Betrag.
    /// </summary>
    public List<TransactionSplit> Splits { get; init; } = [];

    public bool IsCategorized => Splits.Count > 0;

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
    /// Setzt eine Kategorie für den ganzen Betrag. Eine Kategorie macht die Buchung automatisch geprüft;
    /// ohne Kategorie bleibt sie offen, bis jemand sie abhakt.
    /// </summary>
    public void Categorize(int? categoryId) =>
        Split(categoryId is { } id ? [new TransactionSplitDto(id, Betrag)] : []);

    /// <summary>
    /// Ersetzt die Teilbeträge (vorher mit <see cref="ValidateSplit"/> prüfen). Mit Kategorie gilt die
    /// Buchung als geprüft, ohne ist sie wieder offen. Die Teile müssen geladen sein.
    /// </summary>
    public void Split(IReadOnlyList<TransactionSplitDto> parts)
    {
        // Bestehende Zeilen weiterverwenden, damit der eindeutige Index (Buchung, Kategorie) beim Speichern nicht stört.
        Splits.RemoveAll(s => parts.All(p => p.CategoryId != s.CategoryId));
        foreach (var part in parts)
        {
            var existing = Splits.Find(s => s.CategoryId == part.CategoryId);
            if (existing is null)
            {
                Splits.Add(new TransactionSplit { CategoryId = part.CategoryId, Betrag = part.Betrag });
            }
            else
            {
                existing.Betrag = part.Betrag;
            }
        }

        Reviewed = Splits.Count > 0;
    }

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>
    /// Prüft eine Aufteilung: jede Kategorie einmal, kein Teil 0 €, alle mit dem Vorzeichen der Buchung,
    /// höchstens zwei Nachkommastellen, Summe genau der Buchungsbetrag. Leer ist erlaubt (ohne Kategorie).
    /// </summary>
    /// <returns>Deutsche Fehlermeldung oder null.</returns>
    public static string? ValidateSplit(decimal betrag, IReadOnlyList<TransactionSplitDto> parts)
    {
        if (parts.Count == 0)
        {
            return null;
        }

        if (parts.Select(p => p.CategoryId).Distinct().Count() != parts.Count)
        {
            return "Jede Kategorie darf nur einmal vorkommen.";
        }

        if (betrag == 0)
        {
            return parts.Count == 1 && parts[0].Betrag == 0 ? null : "Eine Buchung über 0 € lässt sich nicht aufteilen.";
        }

        if (parts.Any(p => p.Betrag == 0 || Math.Sign(p.Betrag) != Math.Sign(betrag)))
        {
            return "Jeder Teilbetrag muss größer als 0 € sein.";
        }

        if (parts.Any(p => decimal.Round(p.Betrag, 2) != p.Betrag))
        {
            return "Teilbeträge haben höchstens zwei Nachkommastellen.";
        }

        var sum = parts.Sum(p => p.Betrag);
        return sum == betrag
            ? null
            : $"Die Teilbeträge ergeben {Math.Abs(sum).ToString("C", German)}, die Buchung hat {Math.Abs(betrag).ToString("C", German)}.";
    }
}

/// <summary>
/// Teil einer Buchung, der einer Kategorie zugeordnet ist. Der Betrag hat dasselbe Vorzeichen wie die Buchung.
/// </summary>
public sealed record TransactionSplit : Entity
{
    public int TransactionId { get; init; }

    public Transaction? Transaction { get; init; }

    public int CategoryId { get; init; }

    public Category? Category { get; init; }

    public decimal Betrag { get; set; }
}
