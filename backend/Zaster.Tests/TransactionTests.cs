using System;
using Zaster.Transactions;

namespace Zaster.Tests;

public sealed class TransactionTests
{
    private static Transaction Buchung() => new()
    {
        Buchung = new DateOnly(2026, 5, 4),
        Valuta = new DateOnly(2026, 5, 4),
        Auftraggeber = "Rewe",
        Buchungstext = "Lastschrift",
        Betrag = -1m,
    };

    [Fact]
    public void Kategorie_macht_geprueft_und_entfernen_wieder_offen()
    {
        var transaction = Buchung();

        transaction.Categorize(3);
        Assert.True(transaction.Reviewed);

        transaction.Categorize(null);
        Assert.False(transaction.Reviewed);
    }
}
