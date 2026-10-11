using System.Collections.Generic;
using System.Linq;
using Zaster.Transactions;

namespace Zaster.Categories;

internal static class RuleEngine
{
    /// <summary>
    /// Weist jeder Buchung ohne Kategorie die Kategorie der ersten passenden Regel zu.
    /// Regeln werden nach <see cref="Rule.SortOrder"/> geprüft; eine gesetzte Kategorie wird nie überschrieben.
    /// </summary>
    /// <returns>Anzahl der kategorisierten Buchungen.</returns>
    public static int Apply(IEnumerable<Transaction> transactions, IReadOnlyList<Rule> rules)
    {
        var ordered = rules.OrderBy(r => r.SortOrder).ThenBy(r => r.Id).ToList();
        var updated = 0;

        foreach (var transaction in transactions.Where(t => t.CategoryId is null))
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
