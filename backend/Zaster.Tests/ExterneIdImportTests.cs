using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Zaster.Accounts;
using Zaster.Database;
using Zaster.Notifications;
using Zaster.Transactions;

namespace Zaster.Tests;

/// <summary>
/// Import mit eigener Kennung der Bank (Trade Republic: <c>transaction_id</c>) und Zusatzfeldern.
/// </summary>
public sealed class ExterneIdImportTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 3, 2);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _context;
    private readonly TransactionImporter _importer;
    private readonly Account _account;

    public ExterneIdImportTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _account = new Account { Name = "Verrechnungskonto", Iban = "DE02120300000000202051" };
        _context.Accounts.Add(_account);
        _context.SaveChanges();
        _importer = new TransactionImporter(_context, TimeProvider.System, new OpenTransactionsQueue());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static ImportTransaction Kauf(string id, decimal betrag = -100.50m) =>
        new(Day, Day, "Musteraktie AG", "Kauf", null, betrag, ExterneId: id, Isin: "DE0000000001", Stueck: 2m, Kurs: 49.75m, Gebuehr: -1m);

    private Task<ImportResult> Import(params ImportTransaction[] items) =>
        _importer.ImportAsync(_account.Id, items, TransactionSource.Csv, CancellationToken.None);

    [Fact]
    public async Task Gleich_aussehende_Buchungen_mit_verschiedener_Id_werden_beide_uebernommen()
    {
        var result = await Import(Kauf("a"), Kauf("b"));

        Assert.Equal(2, result.Imported);
    }

    [Fact]
    public async Task Erneuter_Import_erkennt_Buchungen_an_der_Id()
    {
        await Import(Kauf("a"), Kauf("b"));

        var result = await Import(Kauf("a"), Kauf("b"), Kauf("c"));

        Assert.Equal(1, result.Imported);
        Assert.Equal(2, result.Skipped);
        Assert.Equal(3, await _context.Transactions.CountAsync());
    }

    [Fact]
    public async Task Vorhandene_Buchung_ohne_Id_wird_ueber_den_Schluessel_erkannt()
    {
        // Etwa von Hand angelegt, bevor es den Import gab.
        await Import(Kauf("a") with { ExterneId = null });

        var result = await Import(Kauf("a"));

        Assert.Equal(0, result.Imported);
    }

    [Fact]
    public async Task Vorhandene_Buchung_mit_anderer_Id_ist_kein_Duplikat()
    {
        await Import(Kauf("a"));

        var result = await Import(Kauf("b") with { ExterneId = null }, Kauf("c"));

        // Die Buchung ohne Id passt nicht zu „a“, weil „a“ eine eigene Kennung hat.
        Assert.Equal(2, result.Imported);
    }

    [Fact]
    public async Task Zusatzfelder_werden_gespeichert()
    {
        var zeitpunkt = new DateTime(2026, 3, 2, 9, 30, 0, DateTimeKind.Utc);
        await Import(Kauf("a") with
        {
            Zeitpunkt = zeitpunkt,
            Steuer = -0.25m,
            Fremdbetrag = 1.5m,
            Fremdwaehrung = "USD",
            Devisenkurs = 1.08m,
            GegenIban = "DE02120300000000202051",
            Art = "BUY",
            Anlageklasse = "STOCK",
            Mcc = "5411",
        });

        var t = await _context.Transactions.SingleAsync();
        Assert.Equal("a", t.ExterneId);
        Assert.Equal(zeitpunkt, t.Zeitpunkt);
        Assert.Equal(-1m, t.Gebuehr);
        Assert.Equal(-0.25m, t.Steuer);
        Assert.Equal("DE0000000001", t.Isin);
        Assert.Equal(2m, t.Stueck);
        Assert.Equal(49.75m, t.Kurs);
        Assert.Equal(1.5m, t.Fremdbetrag);
        Assert.Equal("USD", t.Fremdwaehrung);
        Assert.Equal(1.08m, t.Devisenkurs);
        Assert.Equal("DE02120300000000202051", t.GegenIban);
        Assert.Equal("BUY", t.Art);
        Assert.Equal("STOCK", t.Anlageklasse);
        Assert.Equal("5411", t.Mcc);
    }

    [Fact]
    public async Task Zeitpunkt_kommt_als_UTC_zurueck()
    {
        await Import(Kauf("a") with { Zeitpunkt = new DateTime(2026, 3, 2, 9, 30, 0, DateTimeKind.Utc) });

        _context.ChangeTracker.Clear();
        var dto = TransactionDto.From(await _context.Transactions.SingleAsync());

        Assert.Equal(DateTimeKind.Utc, dto.Zeitpunkt!.Value.Kind);
    }

    [Fact]
    public async Task Buchung_ohne_Geldbewegung_gilt_als_geprueft()
    {
        await Import(Kauf("split", 0m) with { Buchungstext = "Aktiensplit" }, Kauf("a"));

        var transactions = await _context.Transactions.OrderBy(t => t.Id).ToListAsync();
        Assert.True(transactions[0].Reviewed);
        Assert.False(transactions[1].Reviewed);
    }

    [Fact]
    public void Validate_lehnt_doppelte_Ids_in_einer_Datei_ab()
    {
        Assert.NotNull(TransactionImporter.Validate([Kauf("a"), Kauf(" a ")]));
        Assert.Null(TransactionImporter.Validate([Kauf("a"), Kauf("b"), Kauf("x") with { ExterneId = null }]));
    }

    [Fact]
    public void Validate_lehnt_zu_lange_Felder_ab()
    {
        Assert.NotNull(TransactionImporter.Validate([Kauf("a") with { Art = new string('x', 51) }]));
    }
}
