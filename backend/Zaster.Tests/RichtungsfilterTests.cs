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
using Zaster.Categories;
using Zaster.Database;
using Zaster.Transactions;

namespace Zaster.Tests;

public sealed class RichtungsfilterTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
    private readonly AppDbContext _context;
    private readonly Account _account;

    public RichtungsfilterTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _account = new Account { Name = "Girokonto", Iban = "DE02120300000000202051" };
        _context.Accounts.Add(_account);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Ohne_Richtung_kommen_alle()
    {
        var ausgabe = Add("Musterhandel GmbH", -12.50m);
        var einnahme = Add("Erika Mustermann", 2000m);
        var nullBuchung = Add("Depot", 0m);

        var page = await List(RuleDirection.Beide);

        Assert.Equal([nullBuchung, einnahme, ausgabe], page.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task Nur_Einnahmen()
    {
        Add("Musterhandel GmbH", -12.50m);
        var einnahme = Add("Erika Mustermann", 2000m);
        var klein = Add("Zinsen", 0.01m);
        Add("Depot", 0m);

        var page = await List(RuleDirection.Eingang);

        Assert.Equal([klein, einnahme], page.Items.Select(t => t.Id));
        Assert.Equal(2, page.Total);
        Assert.Equal(2000.01m, page.Einnahmen);
        Assert.Equal(0m, page.Ausgaben);
    }

    [Fact]
    public async Task Nur_Ausgaben()
    {
        var ausgabe = Add("Musterhandel GmbH", -12.50m);
        var miete = Add("Hausverwaltung", -1234.56m);
        Add("Erika Mustermann", 2000m);
        Add("Depot", 0m);

        var page = await List(RuleDirection.Ausgang);

        Assert.Equal([miete, ausgabe], page.Items.Select(t => t.Id));
        Assert.Equal(0m, page.Einnahmen);
        Assert.Equal(-1247.06m, page.Ausgaben);
    }

    [Fact]
    public async Task Laesst_sich_mit_Suche_kombinieren()
    {
        Add("Erika Mustermann", 12.50m);
        var ausgabe = Add("Musterhandel GmbH", -12.50m);

        var page = await List(RuleDirection.Ausgang, search: "12,50");

        Assert.Equal([ausgabe], page.Items.Select(t => t.Id));
    }

    private int Add(string auftraggeber, decimal betrag, string verwendungszweck = "")
    {
        var transaction = new Transaction
        {
            AccountId = _account.Id,
            Buchung = new DateOnly(2026, 10, 9),
            Valuta = new DateOnly(2026, 10, 9),
            Auftraggeber = auftraggeber,
            Buchungstext = "Lastschrift",
            Verwendungszweck = verwendungszweck,
            Betrag = betrag,
            Source = TransactionSource.Manuell,
        };
        _context.Transactions.Add(transaction);
        _context.SaveChanges();
        return transaction.Id;
    }

    private async Task<TransactionPage> List(RuleDirection direction, string? search = null)
    {
        _context.ChangeTracker.Clear();
        var controller = new TransactionsController(_context, null!, TimeProvider.System)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = _services },
            },
        };
        return await controller.GetTransactions(_account.Id, new TransactionQuery(null, null, search, null, Direction: direction), CancellationToken.None);
    }
}
