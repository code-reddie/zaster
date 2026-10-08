using System.Collections.Generic;
using System.Linq;
using Zaster.Models;

namespace Zaster.Categorization;

internal static class RuleEngine
{
    /// <summary>
    /// Weist jeder unkategorisierten Buchung die Kategorie der ersten passenden Regel zu.
    /// Regeln werden in Reihenfolge ihrer Id geprüft.
    /// </summary>
    /// <returns>Anzahl der kategorisierten Buchungen.</returns>
    public static int Apply(IEnumerable<Transaction> transactions, IReadOnlyList<CategorizationRule> rules)
    {
        var ordered = rules.OrderBy(r => r.Id).ToList();
        var updated = 0;

        foreach (var transaction in transactions.Where(t => t.CategoryId is null))
        {
            var rule = ordered.FirstOrDefault(r => r.Matches(transaction));
            if (rule is not null)
            {
                transaction.CategoryId = rule.CategoryId;
                updated++;
            }
        }

        return updated;
    }
}
