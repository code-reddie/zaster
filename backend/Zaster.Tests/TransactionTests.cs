using System;
using System.Linq;
using Zaster.Transactions;

namespace Zaster.Tests;

public sealed class TransactionTests
{
    private static Transaction Buchung(decimal betrag = -1m) => new()
    {
        Buchung = new DateOnly(2026, 5, 4),
        Valuta = new DateOnly(2026, 5, 4),
        Auftraggeber = "Rewe",
        Buchungstext = "Lastschrift",
        Betrag = betrag,
    };

    private static TransactionSplitDto Teil(int categoryId, decimal betrag) => new(categoryId, betrag);

    [Fact]
    public void Kategorie_macht_geprueft_und_entfernen_wieder_offen()
    {
        var transaction = Buchung();

        transaction.Categorize(3);
        Assert.True(transaction.Reviewed);
        var split = Assert.Single(transaction.Splits);
        Assert.Equal(3, split.CategoryId);
        Assert.Equal(-1m, split.Betrag);

        transaction.Categorize(null);
        Assert.False(transaction.Reviewed);
        Assert.Empty(transaction.Splits);
    }

    [Fact]
    public void Aufteilen_ersetzt_Teile_und_behaelt_bestehende_Zeilen()
    {
        var transaction = Buchung(-85.40m);
        transaction.Categorize(1);
        var lebensmittel = transaction.Splits[0];

        transaction.Split([Teil(1, -35.41m), Teil(2, -49.99m)]);

        Assert.True(transaction.Reviewed);
        Assert.Same(lebensmittel, transaction.Splits.Single(s => s.CategoryId == 1));
        Assert.Equal(-35.41m, lebensmittel.Betrag);
        Assert.Equal(-49.99m, transaction.Splits.Single(s => s.CategoryId == 2).Betrag);

        transaction.Split([Teil(2, -85.40m)]);
        Assert.Equal(2, Assert.Single(transaction.Splits).CategoryId);
    }

    [Fact]
    public void Gueltige_Aufteilungen()
    {
        Assert.Null(Transaction.ValidateSplit(-85.40m, []));
        Assert.Null(Transaction.ValidateSplit(-85.40m, [Teil(1, -35.41m), Teil(2, -49.99m)]));
        Assert.Null(Transaction.ValidateSplit(100m, [Teil(1, 60m), Teil(2, 40m)]));
        Assert.Null(Transaction.ValidateSplit(0m, [Teil(1, 0m)]));
    }

    [Theory]
    [InlineData(-35.41, -40, "ergeben 75,41 €, die Buchung hat 85,40 €")]
    [InlineData(-90, 4.6, "größer als 0")]
    [InlineData(-85.40, 0, "größer als 0")]
    [InlineData(-35.405, -49.995, "zwei Nachkommastellen")]
    public void Ungueltige_Aufteilungen(decimal erster, decimal zweiter, string meldung)
    {
        var error = Transaction.ValidateSplit(-85.40m, [Teil(1, erster), Teil(2, zweiter)]);

        Assert.NotNull(error);
        Assert.Contains(meldung, error);
    }

    [Fact]
    public void Jede_Kategorie_nur_einmal()
    {
        Assert.Contains("nur einmal", Transaction.ValidateSplit(-2m, [Teil(1, -1m), Teil(1, -1m)]));
    }

    [Fact]
    public void Buchung_ueber_null_laesst_sich_nicht_aufteilen()
    {
        Assert.NotNull(Transaction.ValidateSplit(0m, [Teil(1, 0m), Teil(2, 0m)]));
    }
}
