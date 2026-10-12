using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaster.Common;
using Zaster.Database;

namespace Zaster.Reports;

[Route("api/accounts/{accountId:int}/flows")]
[AccountMember]
public sealed class FlowsController(AppDbContext context) : ApiControllerBase
{
    /// <summary>
    /// Ein- und Ausgaben je Kategorie im Zeitraum (beide Grenzen einschließlich, ohne Grenze = alles),
    /// für das Sankey-Diagramm. Den Kategorienbaum baut das Frontend.
    /// </summary>
    [HttpGet]
    public async Task<FlowsDto> GetFlows(int accountId, [FromQuery] FlowsQuery query, CancellationToken cancellationToken)
    {
        var transactions = context.Transactions.Where(t => t.AccountId == accountId);

        if (query.From is { } from)
        {
            transactions = transactions.Where(t => t.Buchung >= from);
        }

        if (query.To is { } to)
        {
            transactions = transactions.Where(t => t.Buchung <= to);
        }

        // SQLite rechnet nicht mit decimal, die Summen entstehen daher im Speicher.
        var rows = await transactions
            .Select(t => new
            {
                t.Betrag,
                Splits = t.Splits.Select(s => new { s.CategoryId, s.Betrag }).ToList(),
            })
            .ToListAsync(cancellationToken);

        return Flows.Summarize(rows.Select(r =>
            new Flows.Entry(r.Betrag, r.Splits.Select(s => new Flows.Part(s.CategoryId, s.Betrag)).ToList())));
    }
}
