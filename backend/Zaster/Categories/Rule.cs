using System;
using System.Text.Json.Serialization;
using Zaster.Accounts;
using Zaster.Database;
using Zaster.Transactions;

namespace Zaster.Categories;

[JsonConverter(typeof(JsonStringEnumConverter<RuleField>))]
public enum RuleField
{
    Alle,
    Auftraggeber,
    Buchungstext,
    Verwendungszweck
}

/// <summary>
/// Für welche Buchungen eine Regel gilt: Eingänge (Betrag größer 0), Ausgänge (kleiner 0) oder beide.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RuleDirection>))]
public enum RuleDirection
{
    Beide,
    Eingang,
    Ausgang
}

/// <summary>
/// Ordnet Buchungen eines Kontos automatisch einer Kategorie zu, wenn ein Text vorkommt.
/// </summary>
public sealed record Rule : Entity
{
    public int AccountId { get; init; }

    public Account? Account { get; init; }

    public required string Pattern { get; set; }

    public RuleField Field { get; set; }

    public RuleDirection Direction { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; init; }

    /// <summary>
    /// Reihenfolge der Prüfung, kleinere zuerst. Die erste passende Regel gewinnt.
    /// </summary>
    public int SortOrder { get; set; }

    public bool Matches(Transaction transaction) => MatchesDirection(transaction.Betrag) && MatchesText(transaction);

    private bool MatchesDirection(decimal betrag) => Direction switch
    {
        RuleDirection.Eingang => betrag > 0,
        RuleDirection.Ausgang => betrag < 0,
        _ => true
    };

    private bool MatchesText(Transaction transaction) => Field switch
    {
        RuleField.Auftraggeber => Contains(transaction.Auftraggeber),
        RuleField.Buchungstext => Contains(transaction.Buchungstext),
        RuleField.Verwendungszweck => Contains(transaction.Verwendungszweck),
        _ => Contains(transaction.Auftraggeber)
            || Contains(transaction.Buchungstext)
            || Contains(transaction.Verwendungszweck)
    };

    private bool Contains(string value) => value.Contains(Pattern, StringComparison.OrdinalIgnoreCase);
}
