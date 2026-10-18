using System;
using System.Linq;
using Zaster.Backup;

namespace Zaster.Tests;

public sealed class BackupRetentionTests
{
    [Fact]
    public void Dateiname_mit_Zeitstempel_laesst_sich_zuruecklesen()
    {
        var time = new DateTime(2026, 10, 18, 3, 0, 5);
        var name = BackupRetention.FileName(time);

        Assert.Equal("zaster-2026-10-18_03-00-05.db", name);
        Assert.True(BackupRetention.TryParse(name, out var parsed));
        Assert.Equal(time, parsed);
    }

    [Theory]
    [InlineData("zaster.db")]
    [InlineData("zaster-2026-10-18_03-00-05.db.tmp")]
    [InlineData("zaster-2026-10-18.db")]
    [InlineData("kopie-zaster-2026-10-18_03-00-05.db")]
    [InlineData("zaster-2026-13-18_03-00-05.db")]
    public void Fremde_Dateien_sind_keine_Sicherungen(string name)
    {
        Assert.False(BackupRetention.TryParse(name, out _));
    }

    [Fact]
    public void Taeglich_ueber_ein_Jahr_bleiben_5_Tage_5_Wochen_5_Monate()
    {
        // Täglich 03:00 vom 1.1. bis Sonntag, 18.10.2026.
        var end = new DateTime(2026, 10, 18, 3, 0, 0);
        var all = Enumerable.Range(0, 291).Select(i => end.AddDays(-i)).ToList();

        var keep = all.Except(BackupRetention.Obsolete(all, t => t)).OrderByDescending(t => t).ToList();

        var expected = new[]
        {
            // Tage 18.–14.10.; der 18.10. ist zugleich neueste der Woche 42 und des Oktobers.
            new DateTime(2026, 10, 18), new DateTime(2026, 10, 17), new DateTime(2026, 10, 16),
            new DateTime(2026, 10, 15), new DateTime(2026, 10, 14),
            // Sonntage der Wochen 41–38.
            new DateTime(2026, 10, 11), new DateTime(2026, 10, 4), new DateTime(2026, 9, 27), new DateTime(2026, 9, 20),
            // Monatsletzte September (= 30.9.), August, Juli, Juni.
            new DateTime(2026, 9, 30), new DateTime(2026, 8, 31), new DateTime(2026, 7, 31), new DateTime(2026, 6, 30),
        }.Select(d => d.AddHours(3)).OrderByDescending(t => t);

        Assert.Equal(expected, keep);
    }

    [Fact]
    public void Mehrere_am_Tag_nur_die_neueste_bleibt()
    {
        var day = new DateTime(2026, 10, 18);
        var all = new[] { day.AddHours(3), day.AddHours(9), day.AddHours(21) };

        Assert.Equal([day.AddHours(9), day.AddHours(3)], BackupRetention.Obsolete(all, t => t));
    }

    [Fact]
    public void Luecken_zaehlen_nicht_als_Tag()
    {
        // Nur 3 Sicherungen, Monate auseinander: alle bleiben.
        var all = new[] { new DateTime(2026, 1, 5), new DateTime(2026, 5, 5), new DateTime(2026, 10, 5) };

        Assert.Empty(BackupRetention.Obsolete(all, t => t));
    }
}
