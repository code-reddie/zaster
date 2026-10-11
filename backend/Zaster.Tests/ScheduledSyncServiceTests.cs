using System;
using Zaster.FinTs;

namespace Zaster.Tests;

public sealed class ScheduledSyncServiceTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    [Fact]
    public void Naechster_Lauf_heute_wenn_Uhrzeit_noch_kommt()
    {
        var now = new DateTimeOffset(2026, 10, 11, 1, 0, 0, TimeSpan.Zero); // 03:00 in Berlin (Sommerzeit)

        var next = ScheduledSyncService.NextRun(now, new TimeOnly(4, 0), Berlin);

        Assert.Equal(new DateTimeOffset(2026, 10, 11, 2, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void Naechster_Lauf_morgen_wenn_Uhrzeit_vorbei()
    {
        var now = new DateTimeOffset(2026, 10, 11, 6, 0, 0, TimeSpan.Zero);

        var next = ScheduledSyncService.NextRun(now, new TimeOnly(4, 0), Berlin);

        Assert.Equal(new DateTimeOffset(2026, 10, 12, 2, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void Zeitumstellung_ueberspringt_fehlende_Stunde()
    {
        // 29.03.2026: in Berlin springt die Uhr von 02:00 auf 03:00, 02:30 gibt es nicht.
        var now = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);

        var next = ScheduledSyncService.NextRun(now, new TimeOnly(2, 30), Berlin);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void Faellig_wenn_Uhrzeit_im_Zeitraum_liegt()
    {
        var from = new DateTimeOffset(2026, 10, 11, 6, 59, 1, TimeSpan.Zero); // 08:59:01 in Berlin
        var to = from.AddMinutes(1);

        Assert.True(ScheduledSyncService.IsDue(["09:00", "14:00"], from, to, Berlin));
        Assert.False(ScheduledSyncService.IsDue(["14:00"], from, to, Berlin));
        Assert.False(ScheduledSyncService.IsDue([], from, to, Berlin));
    }

    [Fact]
    public void Nicht_faellig_wenn_Uhrzeit_genau_am_Anfang_liegt()
    {
        // Die Minute davor hat 09:00 schon erfasst, der Abruf darf nicht doppelt laufen.
        var from = new DateTimeOffset(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);

        Assert.False(ScheduledSyncService.IsDue(["09:00"], from, from.AddMinutes(1), Berlin));
        Assert.True(ScheduledSyncService.IsDue(["09:00"], from.AddMinutes(-1), from, Berlin));
    }

    [Fact]
    public void Faellig_nach_langer_Pause_nur_einmal_pro_Pruefung()
    {
        // Der Rechner hat geschlafen: 09:00 und 14:00 liegen im Zeitraum, es gibt trotzdem nur einen Abruf.
        var from = new DateTimeOffset(2026, 10, 11, 5, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 10, 11, 13, 0, 0, TimeSpan.Zero);

        Assert.True(ScheduledSyncService.IsDue(["09:00", "14:00"], from, to, Berlin));
    }

    [Fact]
    public void Ungueltige_Uhrzeiten_werden_ignoriert()
    {
        var from = new DateTimeOffset(2026, 10, 11, 6, 59, 1, TimeSpan.Zero);

        Assert.False(ScheduledSyncService.IsDue(["9 Uhr"], from, from.AddMinutes(1), Berlin));
    }

    [Fact]
    public void Uhrzeiten_werden_sortiert_und_doppelte_entfernt()
    {
        Assert.Equal(["09:00", "14:00"], SyncTimes.Normalize(["14:00", "9:00", "09:00"]));
        Assert.Empty(SyncTimes.Normalize([])!);
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("abc")]
    [InlineData("")]
    public void Ungueltige_Uhrzeit_wird_abgelehnt(string value)
    {
        Assert.Null(SyncTimes.Normalize([value]));
    }
}
