using System;
using System.Linq;
using Zaster.Transactions;

namespace Zaster.Tests;

public sealed class TransactionMatcherTests
{
    private static readonly DateOnly Day = new(2026, 5, 4);

    [Fact]
    public void Key_ignoriert_Schreibweise_und_Laenge_des_Auftraggebers()
    {
        // FinTS kürzt auf 27 Zeichen und schreibt Umlaute anders als die CSV.
        var csv = TransactionMatcher.Key(Day, -12.5m, "Bäckerei Müller-Lüdenscheidt GmbH & Co. KG");
        var fints = TransactionMatcher.Key(Day, -12.50m, "BAECKEREI MUELLER-LUEDENSCHEI");

        Assert.Equal(csv, fints);
    }

    [Fact]
    public void Key_unterscheidet_Datum_und_Betrag()
    {
        var key = TransactionMatcher.Key(Day, -12.5m, "Rewe");

        Assert.NotEqual(key, TransactionMatcher.Key(Day.AddDays(1), -12.5m, "Rewe"));
        Assert.NotEqual(key, TransactionMatcher.Key(Day, -12.51m, "Rewe"));
    }

    [Fact]
    public void SelectNew_ueberspringt_vorhandene_und_zaehlt_gleiche_Buchungen()
    {
        var existing = new[] { "a", "a", "b" };
        var incoming = new[] { "a", "a", "a", "b", "c" };

        var result = TransactionMatcher.SelectNew(existing, incoming, k => k);

        // Zwei gleiche Kaffeekäufe liegen schon vor, der dritte ist neu.
        Assert.Equal(["a", "c"], result.ToArray());
    }
}
