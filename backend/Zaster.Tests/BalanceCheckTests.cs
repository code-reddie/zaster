using System;
using System.Linq;
using Zaster.Transactions;
using Entry = Zaster.Transactions.BalanceCheck.Entry;

namespace Zaster.Tests;

public sealed class BalanceCheckTests
{
    private static readonly DateOnly Day1 = new(2026, 5, 4);
    private static readonly DateOnly Day2 = new(2026, 5, 5);
    private static readonly DateOnly Day3 = new(2026, 5, 7);

    private static Entry Csv(DateOnly day, decimal betrag, decimal? saldo) => new(day, betrag, saldo, TransactionSource.Csv);

    [Fact]
    public void Lueckenlose_Buchungen_ergeben_keine_Luecke_und_den_letzten_Kontostand()
    {
        // Reihenfolge wie in der CSV der Bank: neueste zuerst.
        var result = BalanceCheck.Check(
        [
            Csv(Day3, -20m, 1080m),
            Csv(Day2, 200m, 1100m),
            Csv(Day2, -50m, 900m),
            Csv(Day1, -50m, 950m),
        ]);

        Assert.Empty(result.Luecken);
        Assert.Equal(1080m, result.Kontostand);
        Assert.Equal(Day3, result.Stand);
        Assert.Equal(4, result.Geprueft);
    }

    [Fact]
    public void Reihenfolge_innerhalb_eines_Tages_spielt_keine_Rolle()
    {
        var entries = new[]
        {
            Csv(Day1, -10m, 990m),
            Csv(Day1, -20m, 970m),
            Csv(Day1, 100m, 1070m),
        };

        Assert.Empty(BalanceCheck.Check(entries).Luecken);
        Assert.Empty(BalanceCheck.Check(entries.Reverse()).Luecken);
        Assert.Equal(1070m, BalanceCheck.Check(entries.Reverse()).Kontostand);
    }

    [Fact]
    public void Fehlende_Buchung_zwischen_zwei_Tagen_wird_mit_Betrag_gemeldet()
    {
        // Zwischen Tag 1 (Endstand 950) und Tag 3 (Anfangsstand 1100) fehlen 150 € Einnahmen.
        var result = BalanceCheck.Check(
        [
            Csv(Day1, -50m, 950m),
            Csv(Day3, -20m, 1080m),
        ]);

        var gap = Assert.Single(result.Luecken);
        Assert.Equal(Day1, gap.Von);
        Assert.Equal(Day3, gap.Bis);
        Assert.Equal(150m, gap.Differenz);
    }

    [Fact]
    public void Fehlende_Buchung_mitten_am_Tag_wird_ohne_Betrag_gemeldet()
    {
        // 1000 → 990 → (fehlt: −20 → 970) → 1070
        var result = BalanceCheck.Check(
        [
            Csv(Day1, -10m, 990m),
            Csv(Day1, 100m, 1070m),
            Csv(Day2, -70m, 1000m),
        ]);

        var gap = Assert.Single(result.Luecken);
        Assert.Null(gap.Von);
        Assert.Equal(Day1, gap.Bis);
        Assert.Null(gap.Differenz);
        Assert.Equal(1000m, result.Kontostand);
    }

    [Fact]
    public void Buchungen_von_Hand_bleiben_aussen_vor()
    {
        var result = BalanceCheck.Check(
        [
            Csv(Day1, -50m, 950m),
            new Entry(Day2, -5m, null, TransactionSource.Manuell),
            Csv(Day3, -20m, 930m),
        ]);

        Assert.Empty(result.Luecken);
        Assert.Equal(0, result.OhneKontostand);
    }

    [Fact]
    public void Bankbuchungen_ohne_Kontostand_werden_zwischen_bekannten_Staenden_mitgezaehlt()
    {
        // Abruf vor der Kontostand-Funktion: Buchung über −30 € ohne Kontostand.
        var result = BalanceCheck.Check(
        [
            Csv(Day1, -50m, 950m),
            new Entry(Day2, -30m, null, TransactionSource.FinTs),
            Csv(Day3, -20m, 900m),
        ]);

        Assert.Empty(result.Luecken);
        Assert.Equal(1, result.OhneKontostand);
    }

    [Fact]
    public void Tag_mit_Buchung_und_Rueckbuchung_schliesst_an_den_Vortag_an()
    {
        var result = BalanceCheck.Check(
        [
            Csv(Day1, -50m, 950m),
            Csv(Day2, 10m, 960m),
            Csv(Day2, -10m, 950m),
            Csv(Day3, -50m, 900m),
        ]);

        Assert.Empty(result.Luecken);
        Assert.Equal(900m, result.Kontostand);
    }

    [Fact]
    public void Ohne_Kontostaende_gibt_es_nichts_zu_pruefen()
    {
        var result = BalanceCheck.Check([new Entry(Day1, -5m, null, TransactionSource.FinTs)]);

        Assert.Null(result.Kontostand);
        Assert.Equal(0, result.Geprueft);
        Assert.Empty(result.Luecken);
    }
}
