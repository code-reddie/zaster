using System.Text.Json.Serialization;

namespace Zaster.Models;

[JsonConverter(typeof(JsonStringEnumConverter<RuleField>))]
public enum RuleField
{
    Alle,
    Auftragsgeber,
    Buchungstext,
    Verwendungszweck
}

public sealed record CategorizationRule : Entity
{
    public required string Pattern { get; init; }

    public RuleField Field { get; init; }

    public int CategoryId { get; init; }

    public Category? Category { get; init; }

    public bool Matches(Transaction transaction)
    {
        return Field switch
        {
            RuleField.Auftragsgeber => Contains(transaction.Auftragsgeber),
            RuleField.Buchungstext => Contains(transaction.Buchungstext),
            RuleField.Verwendungszweck => Contains(transaction.Verwendungszweck),
            _ => Contains(transaction.Auftragsgeber)
                || Contains(transaction.Buchungstext)
                || Contains(transaction.Verwendungszweck)
        };
    }

    private bool Contains(string value) =>
        value.Contains(Pattern, System.StringComparison.OrdinalIgnoreCase);
}

public sealed record CategorizationRuleDto(int Id, string Pattern, RuleField Field, int CategoryId);

public sealed record CreateCategorizationRule(string Pattern, RuleField Field, int CategoryId);

public sealed record ApplyRulesResult(int Updated);
