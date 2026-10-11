using System;
using Zaster.Categories;
using Zaster.Transactions;

namespace Zaster.Tests;

public sealed class RuleEngineTests
{
    private static Transaction Buchung(string auftraggeber, string verwendungszweck = "", int? categoryId = null) => new()
    {
        Buchung = new DateOnly(2026, 5, 4),
        Valuta = new DateOnly(2026, 5, 4),
        Auftraggeber = auftraggeber,
        Buchungstext = "Lastschrift",
        Verwendungszweck = verwendungszweck,
        Betrag = -1m,
        CategoryId = categoryId,
    };

    [Fact]
    public void Erste_passende_Regel_gewinnt_und_Gross_Kleinschreibung_egal()
    {
        var transaction = Buchung("REWE Markt GmbH");
        var rules = new[]
        {
            new Rule { Id = 1, Pattern = "markt", Field = RuleField.Alle, CategoryId = 20, SortOrder = 1 },
            new Rule { Id = 2, Pattern = "rewe", Field = RuleField.Auftraggeber, CategoryId = 10, SortOrder = 0 },
        };

        var updated = RuleEngine.Apply([transaction], rules);

        Assert.Equal(1, updated);
        Assert.Equal(10, transaction.CategoryId);
        Assert.True(transaction.Reviewed);
    }

    [Fact]
    public void Feld_wird_beachtet()
    {
        var transaction = Buchung("Amazon", verwendungszweck: "Rewe Gutschein");
        var rules = new[] { new Rule { Id = 1, Pattern = "rewe", Field = RuleField.Auftraggeber, CategoryId = 10 } };

        Assert.Equal(0, RuleEngine.Apply([transaction], rules));
        Assert.Null(transaction.CategoryId);
        Assert.False(transaction.Reviewed);
    }

    [Fact]
    public void Gesetzte_Kategorie_wird_nie_ueberschrieben()
    {
        var transaction = Buchung("Rewe", categoryId: 99);
        var rules = new[] { new Rule { Id = 1, Pattern = "rewe", Field = RuleField.Alle, CategoryId = 10 } };

        Assert.Equal(0, RuleEngine.Apply([transaction], rules));
        Assert.Equal(99, transaction.CategoryId);
    }
}
