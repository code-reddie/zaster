using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zaster.Accounts;
using Zaster.Database;
using Zaster.Notifications;
using Zaster.Transactions;

namespace Zaster.Tests;

public sealed class BemerkungTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
    private readonly AppDbContext _context;
    private readonly Account _account;
    private readonly Transaction _transaction;

    public BemerkungTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _account = new Account { Name = "Girokonto", Iban = "DE02120300000000202051" };
        _context.Accounts.Add(_account);
        _context.SaveChanges();

        _transaction = new Transaction
        {
            AccountId = _account.Id,
            Buchung = new DateOnly(2026, 10, 9),
            Valuta = new DateOnly(2026, 10, 9),
            Auftraggeber = "Musterhandel GmbH",
            Buchungstext = "Lastschrift",
            Verwendungszweck = "RE 4711",
            Betrag = -49.99m,
            Source = TransactionSource.FinTs,
        };
        _context.Transactions.Add(_transaction);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Bemerkung_wird_gespeichert_und_getrimmt()
    {
        var result = await Controller().SetBemerkung(_account.Id, _transaction.Id, new SetBemerkung("  Geschenk für Oma  "), CancellationToken.None);

        Assert.Equal("Geschenk für Oma", result.Value!.Bemerkung);
        Assert.Equal("Geschenk für Oma", await StoredBemerkung());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Leere_Bemerkung_entfernt_sie(string? value)
    {
        await Controller().SetBemerkung(_account.Id, _transaction.Id, new SetBemerkung("Geschenk"), CancellationToken.None);

        var result = await Controller().SetBemerkung(_account.Id, _transaction.Id, new SetBemerkung(value), CancellationToken.None);

        Assert.Null(result.Value!.Bemerkung);
        Assert.Null(await StoredBemerkung());
    }

    [Fact]
    public async Task Zu_lange_Bemerkung_wird_abgelehnt()
    {
        var result = await Controller().SetBemerkung(
            _account.Id, _transaction.Id, new SetBemerkung(new string('x', Transaction.MaxBemerkung + 1)), CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        Assert.Null(await StoredBemerkung());
    }

    [Fact]
    public async Task Bemerkung_aendert_nichts_an_geprueft()
    {
        var result = await Controller().SetBemerkung(_account.Id, _transaction.Id, new SetBemerkung("Geschenk"), CancellationToken.None);

        Assert.False(result.Value!.Reviewed);
    }

    [Fact]
    public async Task Suche_findet_Bemerkung()
    {
        await Controller().SetBemerkung(_account.Id, _transaction.Id, new SetBemerkung("Geschenk für Oma"), CancellationToken.None);

        var page = await Controller().GetTransactions(_account.Id, new TransactionQuery(null, null, "oma", null), CancellationToken.None);

        Assert.Equal([_transaction.Id], page.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task Import_eines_Duplikats_behaelt_die_Bemerkung()
    {
        await Controller().SetBemerkung(_account.Id, _transaction.Id, new SetBemerkung("Geschenk"), CancellationToken.None);
        _context.ChangeTracker.Clear();

        var importer = new TransactionImporter(_context, TimeProvider.System, new OpenTransactionsQueue());
        var result = await importer.ImportAsync(
            _account.Id,
            [new ImportTransaction(new DateOnly(2026, 10, 9), null, "Musterhandel GmbH", "Lastschrift", "RE 4711 anders formatiert", -49.99m, 120m)],
            TransactionSource.Csv,
            CancellationToken.None);

        Assert.Equal(new ImportResult(0, 1), result);
        Assert.Equal("Geschenk", await StoredBemerkung());
    }

    private async Task<string?> StoredBemerkung()
    {
        _context.ChangeTracker.Clear();
        return await _context.Transactions.Where(t => t.Id == _transaction.Id).Select(t => t.Bemerkung).SingleAsync();
    }

    private TransactionsController Controller()
    {
        _context.ChangeTracker.Clear();
        return new TransactionsController(_context, null!, TimeProvider.System)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = _services },
            },
        };
    }
}
