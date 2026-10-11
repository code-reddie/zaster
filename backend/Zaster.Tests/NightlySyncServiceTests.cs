using System;
using Zaster.FinTs;

namespace Zaster.Tests;

public sealed class NightlySyncServiceTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    [Fact]
    public void Naechster_Lauf_heute_wenn_Uhrzeit_noch_kommt()
    {
        var now = new DateTimeOffset(2026, 10, 11, 1, 0, 0, TimeSpan.Zero); // 03:00 in Berlin (Sommerzeit)

        var next = NightlySyncService.NextRun(now, new TimeOnly(4, 0), Berlin);

        Assert.Equal(new DateTimeOffset(2026, 10, 11, 2, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void Naechster_Lauf_morgen_wenn_Uhrzeit_vorbei()
    {
        var now = new DateTimeOffset(2026, 10, 11, 6, 0, 0, TimeSpan.Zero);

        var next = NightlySyncService.NextRun(now, new TimeOnly(4, 0), Berlin);

        Assert.Equal(new DateTimeOffset(2026, 10, 12, 2, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void Zeitumstellung_ueberspringt_fehlende_Stunde()
    {
        // 29.03.2026: in Berlin springt die Uhr von 02:00 auf 03:00, 02:30 gibt es nicht.
        var now = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);

        var next = NightlySyncService.NextRun(now, new TimeOnly(2, 30), Berlin);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), next);
    }
}
