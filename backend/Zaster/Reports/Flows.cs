using System.Collections.Generic;
using System.Linq;

namespace Zaster.Reports;

/// <summary>
/// Rechnet die Geldflüsse eines Zeitraums je Kategorie zusammen, als Grundlage für das Sankey-Diagramm.
/// </summary>
/// <remarks>
/// Aufgeteilte Buchungen zählen mit ihren Teilbeträgen. Pro Kategorie wird verrechnet: Eine Erstattung
/// unter „Lebensmittel“ senkt die Ausgaben für Lebensmittel, statt als Einnahme aufzutauchen. Was übrig
/// bleibt, steht je nach Vorzeichen bei den Einnahmen oder Ausgaben. Buchungen ohne Kategorie werden
/// nicht verrechnet, da sie nichts miteinander zu tun haben.
/// </remarks>
internal static class Flows
{
    public sealed record Entry(decimal Betrag, IReadOnlyList<Part> Splits);

    public sealed record Part(int CategoryId, decimal Betrag);

    public static FlowsDto Summarize(IEnumerable<Entry> entries)
    {
        var byCategory = new Dictionary<int, decimal>();
        decimal einnahmen = 0;
        decimal ausgaben = 0;
        var count = 0;

        foreach (var entry in entries)
        {
            count++;
            if (entry.Splits.Count == 0)
            {
                if (entry.Betrag > 0)
                {
                    einnahmen += entry.Betrag;
                }
                else
                {
                    ausgaben += entry.Betrag;
                }

                continue;
            }

            foreach (var part in entry.Splits)
            {
                byCategory[part.CategoryId] = byCategory.GetValueOrDefault(part.CategoryId) + part.Betrag;
            }
        }

        var categories = byCategory
            .Where(c => c.Value != 0)
            .OrderBy(c => c.Key)
            .Select(c => new CategoryFlowDto(c.Key, c.Value))
            .ToList();

        return new FlowsDto(categories, einnahmen, ausgaben, count);
    }
}
