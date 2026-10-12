using System;
using System.Collections.Generic;
using System.Linq;
using Zaster.Transactions;

namespace Zaster.Categories;

internal static class RuleEngine
{
    /// <summary>
    /// Weist jeder Buchung ohne Kategorie die Kategorie der passenden Regel zu (für den ganzen Betrag).
    /// Passen mehrere, gewinnt der längste Suchtext (er ist der genauere, „REWE Markt“ vor „REWE“), danach
    /// alphabetisch. Gesetzte Kategorien oder Aufteilungen werden nie überschrieben.
    /// </summary>
    /// <returns>Anzahl der kategorisierten Buchungen.</returns>
    public static int Apply(IEnumerable<Transaction> transactions, IReadOnlyList<Rule> rules)
    {
        var ordered = rules
            .OrderByDescending(r => r.Pattern.Length)
            .ThenBy(r => r.Pattern, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id)
            .ToList();
        var updated = 0;

        foreach (var transaction in transactions.Where(t => !t.IsCategorized))
        {
            var rule = ordered.FirstOrDefault(r => r.Matches(transaction));
            if (rule is not null)
            {
                transaction.Categorize(rule.CategoryId);
                updated++;
            }
        }

        return updated;
    }
}
