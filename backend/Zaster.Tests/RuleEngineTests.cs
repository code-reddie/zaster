using System;
using Zaster.Categorization;
using Zaster.Models;

namespace Zaster.Tests;

public sealed class RuleEngineTests
{
    private static Transaction Buchung(string auftraggeber, string verwendungszweck = "", int? categoryId = null) => new()
    {
        Buchung = DateTimeOffset.UnixEpoch,
        Valuta = DateTimeOffset.UnixEpoch,
        Auftragsgeber = auftraggeber,
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
            new CategorizationRule { Id = 2, Pattern = "markt", Field = RuleField.Alle, CategoryId = 20 },
            new CategorizationRule { Id = 1, Pattern = "rewe", Field = RuleField.Auftragsgeber, CategoryId = 10 },
        };

        var updated = RuleEngine.Apply([transaction], rules);

        Assert.Equal(1, updated);
        Assert.Equal(10, transaction.CategoryId);
    }

    [Fact]
    public void Feld_wird_beachtet()
    {
        var transaction = Buchung("Amazon", verwendungszweck: "Rewe Gutschein");
        var rules = new[] { new CategorizationRule { Id = 1, Pattern = "rewe", Field = RuleField.Auftragsgeber, CategoryId = 10 } };

        Assert.Equal(0, RuleEngine.Apply([transaction], rules));
        Assert.Null(transaction.CategoryId);
    }

    [Fact]
    public void Gesetzte_Kategorie_wird_nie_ueberschrieben()
    {
        var transaction = Buchung("Rewe", categoryId: 99);
        var rules = new[] { new CategorizationRule { Id = 1, Pattern = "rewe", Field = RuleField.Alle, CategoryId = 10 } };

        Assert.Equal(0, RuleEngine.Apply([transaction], rules));
        Assert.Equal(99, transaction.CategoryId);
    }
}
