using System.Linq;

using Zaster.Reports;

namespace Zaster.Tests;

public sealed class FlowsTests
{
    private static Flows.Entry Buchung(decimal betrag, params (int Kategorie, decimal Betrag)[] teile) =>
        new(betrag, [.. teile.Select(t => new Flows.Part(t.Kategorie, t.Betrag))]);

    [Fact]
    public void Summarize_zaehlt_aufgeteilte_Buchungen_je_Teil()
    {
        // Kaufland: 80 € Lebensmittel (1) und 40 € Werkzeuge (2)
        var result = Flows.Summarize([Buchung(-120m, (1, -80m), (2, -40m)), Buchung(-20m, (1, -20m))]);

        Assert.Equal([new CategoryFlowDto(1, -100m), new CategoryFlowDto(2, -40m)], result.Categories);
        Assert.Equal(2, result.Buchungen);
    }

    [Fact]
    public void Summarize_verrechnet_Erstattungen_innerhalb_der_Kategorie()
    {
        // Gehalt (1), Online-Shopping (2) mit Rücksendung, Umbuchung (3) hin und zurück
        var result = Flows.Summarize(
        [
            Buchung(3000m, (1, 3000m)),
            Buchung(-150m, (2, -150m)),
            Buchung(60m, (2, 60m)),
            Buchung(-500m, (3, -500m)),
            Buchung(500m, (3, 500m)),
        ]);

        Assert.Equal([new CategoryFlowDto(1, 3000m), new CategoryFlowDto(2, -90m)], result.Categories);
    }

    [Fact]
    public void Summarize_trennt_Buchungen_ohne_Kategorie_nach_Vorzeichen()
    {
        var result = Flows.Summarize([Buchung(100m), Buchung(-30m), Buchung(-20m), Buchung(0m)]);

        Assert.Empty(result.Categories);
        Assert.Equal(100m, result.OhneKategorieEinnahmen);
        Assert.Equal(-50m, result.OhneKategorieAusgaben);
        Assert.Equal(4, result.Buchungen);
    }
}
