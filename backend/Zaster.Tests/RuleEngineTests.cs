using System;
using System.Linq;
using Zaster.Categories;
using Zaster.Transactions;

namespace Zaster.Tests;

public sealed class RuleEngineTests
{
    private static Transaction Buchung(string auftraggeber, string verwendungszweck = "", int? categoryId = null, decimal betrag = -1m)
    {
        var transaction = new Transaction
        {
            Buchung = new DateOnly(2026, 5, 4),
            Valuta = new DateOnly(2026, 5, 4),
            Auftraggeber = auftraggeber,
            Buchungstext = "Lastschrift",
            Verwendungszweck = verwendungszweck,
            Betrag = betrag,
        };
        transaction.Categorize(categoryId);
        return transaction;
    }

    private static int? CategoryOf(Transaction t) => t.Splits.SingleOrDefault()?.CategoryId;

    [Fact]
    public void Erste_passende_Regel_gewinnt_und_Gross_Kleinschreibung_egal()
    {
        var transaction = Buchung("REWE Markt GmbH");
        var rules = new[]
        {
            new Rule { Id = 1, Conditions = [new RuleCondition { Field = RuleField.Alle, Pattern = "markt" }], CategoryId = 20, SortOrder = 1 },
            new Rule { Id = 2, Conditions = [new RuleCondition { Field = RuleField.Auftraggeber, Pattern = "rewe" }], CategoryId = 10, SortOrder = 0 },
        };

        var updated = RuleEngine.Apply([transaction], rules);

        Assert.Equal(1, updated);
        Assert.Equal(10, CategoryOf(transaction));
        Assert.True(transaction.Reviewed);
    }

    [Fact]
    public void Feld_wird_beachtet()
    {
        var transaction = Buchung("Amazon", verwendungszweck: "Rewe Gutschein");
        var rules = new[] { new Rule { Id = 1, Conditions = [new RuleCondition { Field = RuleField.Auftraggeber, Pattern = "rewe" }], CategoryId = 10 } };

        Assert.Equal(0, RuleEngine.Apply([transaction], rules));
        Assert.Empty(transaction.Splits);
        Assert.False(transaction.Reviewed);
    }

    [Fact]
    public void Gesetzte_Kategorie_wird_nie_ueberschrieben()
    {
        var transaction = Buchung("Rewe", categoryId: 99);
        var rules = new[] { new Rule { Id = 1, Conditions = [new RuleCondition { Field = RuleField.Alle, Pattern = "rewe" }], CategoryId = 10 } };

        Assert.Equal(0, RuleEngine.Apply([transaction], rules));
        Assert.Equal(99, CategoryOf(transaction));
    }

    [Fact]
    public void Aufteilung_wird_nie_ueberschrieben()
    {
        var transaction = Buchung("Rewe");
        transaction.Split([new TransactionSplitDto(1, -0.4m), new TransactionSplitDto(2, -0.6m)]);
        var rules = new[] { new Rule { Id = 1, Conditions = [new RuleCondition { Field = RuleField.Alle, Pattern = "rewe" }], CategoryId = 10 } };

        Assert.Equal(0, RuleEngine.Apply([transaction], rules));
        Assert.Equal([1, 2], transaction.Splits.Select(s => s.CategoryId));
    }

    [Theory]
    [InlineData(RuleDirection.Beide, -1, true)]
    [InlineData(RuleDirection.Beide, 1, true)]
    [InlineData(RuleDirection.Ausgang, -1, true)]
    [InlineData(RuleDirection.Ausgang, 1, false)]
    [InlineData(RuleDirection.Eingang, 1, true)]
    [InlineData(RuleDirection.Eingang, -1, false)]
    [InlineData(RuleDirection.Eingang, 0, false)]
    [InlineData(RuleDirection.Ausgang, 0, false)]
    public void Richtung_wird_beachtet(RuleDirection direction, int betrag, bool matches)
    {
        var transaction = Buchung("Rewe", betrag: betrag);
        var rules = new[] { new Rule { Id = 1, Conditions = [new RuleCondition { Field = RuleField.Alle, Pattern = "rewe" }], Direction = direction, CategoryId = 10 } };

        Assert.Equal(matches ? 1 : 0, RuleEngine.Apply([transaction], rules));
    }

    [Fact]
    public void Regel_der_falschen_Richtung_wird_uebersprungen()
    {
        var erstattung = Buchung("Amazon", betrag: 12.99m);
        var rules = new[]
        {
            new Rule { Id = 1, Conditions = [new RuleCondition { Field = RuleField.Auftraggeber, Pattern = "amazon" }], Direction = RuleDirection.Ausgang, CategoryId = 10, SortOrder = 0 },
            new Rule { Id = 2, Conditions = [new RuleCondition { Field = RuleField.Auftraggeber, Pattern = "amazon" }], Direction = RuleDirection.Eingang, CategoryId = 20, SortOrder = 1 },
        };

        RuleEngine.Apply([erstattung], rules);

        Assert.Equal(20, CategoryOf(erstattung));
    }

    [Fact]
    public void Alle_Bedingungen_muessen_passen()
    {
        var miete = Buchung("Hausverwaltung Muster", verwendungszweck: "Miete Mai");
        var nebenkosten = Buchung("Hausverwaltung Muster", verwendungszweck: "Nebenkosten 2025");
        var fremd = Buchung("Anderer Vermieter", verwendungszweck: "Miete Mai");
        var rules = new[]
        {
            new Rule
            {
                Id = 1,
                CategoryId = 10,
                Conditions =
                [
                    new RuleCondition { Field = RuleField.Auftraggeber, Pattern = "hausverwaltung" },
                    new RuleCondition { Field = RuleField.Verwendungszweck, Pattern = "miete" },
                ],
            },
        };

        Assert.Equal(1, RuleEngine.Apply([miete, nebenkosten, fremd], rules));
        Assert.Equal(10, CategoryOf(miete));
        Assert.Null(CategoryOf(nebenkosten));
        Assert.Null(CategoryOf(fremd));
    }

    [Fact]
    public void Regel_ohne_Bedingung_passt_nie()
    {
        var transaction = Buchung("Rewe");
        var rules = new[] { new Rule { Id = 1, CategoryId = 10 } };

        Assert.Equal(0, RuleEngine.Apply([transaction], rules));
    }
}
