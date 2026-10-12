using System;
using System.Linq;
using Zaster.Categories;
using Zaster.Transactions;

namespace Zaster.Tests;

public sealed class RuleEngineTests
{
    private static Transaction Buchung(string auftraggeber, string verwendungszweck = "", int? categoryId = null)
    {
        var transaction = new Transaction
        {
            Buchung = new DateOnly(2026, 5, 4),
            Valuta = new DateOnly(2026, 5, 4),
            Auftraggeber = auftraggeber,
            Buchungstext = "Lastschrift",
            Verwendungszweck = verwendungszweck,
            Betrag = -1m,
        };
        transaction.Categorize(categoryId);
        return transaction;
    }

    private static int? CategoryOf(Transaction t) => t.Splits.SingleOrDefault()?.CategoryId;

    [Fact]
    public void Laengster_Suchtext_gewinnt_und_Gross_Kleinschreibung_egal()
    {
        var transaction = Buchung("REWE Markt GmbH");
        var rules = new[]
        {
            new Rule { Id = 1, Pattern = "rewe", Field = RuleField.Auftraggeber, CategoryId = 10 },
            new Rule { Id = 2, Pattern = "rewe markt", Field = RuleField.Alle, CategoryId = 20 },
            new Rule { Id = 3, Pattern = "markt", Field = RuleField.Alle, CategoryId = 30 },
        };

        var updated = RuleEngine.Apply([transaction], rules);

        Assert.Equal(1, updated);
        Assert.Equal(20, CategoryOf(transaction));
        Assert.True(transaction.Reviewed);
    }

    [Fact]
    public void Bei_gleich_langem_Suchtext_gewinnt_der_alphabetisch_erste()
    {
        var transaction = Buchung("Lidl und Aldi");
        var rules = new[]
        {
            new Rule { Id = 1, Pattern = "lidl", Field = RuleField.Alle, CategoryId = 10 },
            new Rule { Id = 2, Pattern = "Aldi", Field = RuleField.Alle, CategoryId = 20 },
        };

        RuleEngine.Apply([transaction], rules);

        Assert.Equal(20, CategoryOf(transaction));
    }

    [Fact]
    public void Feld_wird_beachtet()
    {
        var transaction = Buchung("Amazon", verwendungszweck: "Rewe Gutschein");
        var rules = new[] { new Rule { Id = 1, Pattern = "rewe", Field = RuleField.Auftraggeber, CategoryId = 10 } };

        Assert.Equal(0, RuleEngine.Apply([transaction], rules));
        Assert.Empty(transaction.Splits);
        Assert.False(transaction.Reviewed);
    }

    [Fact]
    public void Gesetzte_Kategorie_wird_nie_ueberschrieben()
    {
        var transaction = Buchung("Rewe", categoryId: 99);
        var rules = new[] { new Rule { Id = 1, Pattern = "rewe", Field = RuleField.Alle, CategoryId = 10 } };

        Assert.Equal(0, RuleEngine.Apply([transaction], rules));
        Assert.Equal(99, CategoryOf(transaction));
    }

    [Fact]
    public void Aufteilung_wird_nie_ueberschrieben()
    {
        var transaction = Buchung("Rewe");
        transaction.Split([new TransactionSplitDto(1, -0.4m), new TransactionSplitDto(2, -0.6m)]);
        var rules = new[] { new Rule { Id = 1, Pattern = "rewe", Field = RuleField.Alle, CategoryId = 10 } };

        Assert.Equal(0, RuleEngine.Apply([transaction], rules));
        Assert.Equal([1, 2], transaction.Splits.Select(s => s.CategoryId));
    }
}
