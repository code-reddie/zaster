using System;
using System.Linq;
using libfintx.Swift;
using Zaster.FinTs;

namespace Zaster.Tests;

public sealed class FinTsBalanceTests
{
    private static SwiftStatement Statement(decimal start, decimal end, params decimal[] amounts)
    {
        var statement = new SwiftStatement { StartBalance = start, EndBalance = end };
        foreach (var amount in amounts)
        {
            statement.SwiftTransactions.Add(new SwiftTransaction
            {
                Amount = amount,
                ValueDate = new DateTime(2026, 5, 4),
                EntryDate = new DateTime(2026, 5, 4),
                PartnerName = "Bäckerei",
            });
        }

        return statement;
    }

    [Fact]
    public void Kontostand_wird_aus_Anfangssaldo_und_Reihenfolge_des_Auszugs_berechnet()
    {
        var transactions = FinTsService.ToTransactions(Statement(1000m, 1070m, -10m, -20m, 100m)).ToList();

        Assert.Equal([990m, 970m, 1070m], transactions.Select(t => t.Saldo));
    }

    [Fact]
    public void Kontostand_bleibt_leer_wenn_der_Endsaldo_nicht_aufgeht()
    {
        var transactions = FinTsService.ToTransactions(Statement(1000m, 1234m, -10m, -20m)).ToList();

        Assert.All(transactions, t => Assert.Null(t.Saldo));
    }
}
