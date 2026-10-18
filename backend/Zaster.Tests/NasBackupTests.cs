using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zaster.Accounts;
using Zaster.Backup;
using Zaster.Database;
using Zaster.FinTs;

namespace Zaster.Tests;

public sealed class NasBackupTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _context;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"zaster-nas-{Guid.NewGuid():N}");
    private readonly FixedTime _time = new();

    public NasBackupTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task Standard_ist_taeglich_um_drei()
    {
        var settings = await Backup(_directory).GetSettingsAsync(CancellationToken.None);

        Assert.Equal("03:00", settings.Time);
        Assert.Null(settings.LastBackupAt);
    }

    [Fact]
    public async Task Ohne_Zielordner_passiert_nichts()
    {
        Assert.False(await Backup(null).RunAsync(CancellationToken.None));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Sicherung_bekommt_Zeitstempel_in_Ortszeit()
    {
        AddAccount("Girokonto");
        Assert.True(await Backup(_directory).RunAsync(CancellationToken.None));

        AddAccount("Tagesgeld");
        _time.Now = _time.Now.AddMinutes(1);
        Assert.True(await Backup(_directory).RunAsync(CancellationToken.None));

        // Beide am selben Tag: nur die neuere bleibt.
        var file = Assert.Single(Directory.GetFiles(_directory));
        Assert.Equal("zaster-2026-10-11_03-01-00.db", Path.GetFileName(file));
        using var copy = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={file};Mode=ReadOnly;Pooling=False").Options);
        Assert.Equal(2, await copy.Accounts.CountAsync());

        var settings = await Backup(_directory).GetSettingsAsync(CancellationToken.None);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, settings.LastBackupAt);
        Assert.Null(settings.LastError);
    }

    [Fact]
    public async Task Alte_Sicherungen_werden_nach_Raster_geloescht_fremde_Dateien_bleiben()
    {
        File.WriteAllText(Path.Combine(_directory, "zaster.db"), "alt");
        File.WriteAllText(Path.Combine(_directory, "notizen.txt"), "x");

        for (var day = 0; day < 200; day++)
        {
            Assert.True(await Backup(_directory).RunAsync(CancellationToken.None));
            _time.Now = _time.Now.AddDays(1);
        }

        var backups = Directory.GetFiles(_directory, "zaster-*.db");
        // 5 Tage, dazu die Wochen und Monate davor, die nicht schon dabei sind.
        Assert.InRange(backups.Length, 11, 15);
        Assert.True(File.Exists(Path.Combine(_directory, "zaster.db")));
        Assert.True(File.Exists(Path.Combine(_directory, "notizen.txt")));

        var schedule = await Backup(_directory).GetScheduleAsync(CancellationToken.None);
        Assert.Equal(backups.Length, schedule.Count);
    }

    [Fact]
    public async Task Fehlender_Zielordner_wird_als_Fehler_gemerkt()
    {
        var missing = Path.Combine(_directory, "fehlt");

        Assert.False(await Backup(missing).RunAsync(CancellationToken.None));

        var settings = await Backup(missing).GetSettingsAsync(CancellationToken.None);
        Assert.Contains("gibt es nicht", settings.LastError);
        Assert.Null(settings.LastBackupAt);
    }

    [Theory]
    [InlineData("https://zaster.example.de", "https://zaster.example.de/einstellungen")]
    [InlineData("https://example.de/zaster/", "https://example.de/zaster/einstellungen")]
    public void Benachrichtigung_fuehrt_zu_den_Einstellungen(string zasterUrl, string expected)
    {
        Assert.True(Zaster.Notifications.HomeAssistantClient.TryParseHttpUri(zasterUrl, out var uri));
        Assert.Equal(expected, ScheduledBackupService.SettingsLink(uri).AbsoluteUri);
    }

    private NasBackup Backup(string? directory) =>
        new(_context, Options.Create(new BackupOptions { Directory = directory }), Options.Create(new FinTsOptions()), _time);

    private void AddAccount(string name)
    {
        _context.Accounts.Add(new Account { Name = name, Iban = $"DE{Guid.NewGuid():N}" });
        _context.SaveChanges();
    }

    private sealed class FixedTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 11, 1, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
