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

public sealed class BetragssucheTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
    private readonly AppDbContext _context;
    private readonly Account _account;

    public BetragssucheTests()
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

    [Theory]
    [InlineData("12,50", 12.50, false)]
    [InlineData("12.50", 12.50, false)]
    [InlineData("12,5", 12.5, false)]
    [InlineData("12", 12, false)]
    [InlineData("-12,50", -12.50, true)]
    [InlineData("− 12,50 €", -12.50, true)]
    [InlineData("+12,50", 12.50, true)]
    [InlineData("1.234,56", 1234.56, false)]
    [InlineData("1,234.56", 1234.56, false)]
    [InlineData("1.234", 1234, false)]
    [InlineData("1.234.567", 1234567, false)]
    [InlineData("12,50 EUR", 12.50, false)]
    public void Erkennt_Betraege(string text, double expected, bool expectedSigned)
    {
        Assert.True(AmountSearch.TryParse(text, out var amount, out var signed));
        Assert.Equal((decimal)expected, amount);
        Assert.Equal(expectedSigned, signed);
    }

    [Theory]
    [InlineData("Rewe")]
    [InlineData("RE 4711")]
    [InlineData("12,")]
    [InlineData("12,505")]
    [InlineData("1,2,3")]
    [InlineData("12.34.56")]
    [InlineData("-")]
    [InlineData("")]
    public void Erkennt_keine_Betraege(string text)
    {
        Assert.False(AmountSearch.TryParse(text, out _, out _));
    }

    [Fact]
    public async Task Findet_Betrag_unabhaengig_vom_Vorzeichen()
    {
        var ausgabe = Add("Musterhandel GmbH", -12.50m);
        var einnahme = Add("Erika Mustermann", 12.50m);
        Add("Bäckerei", -12.51m);

        Assert.Equal([einnahme, ausgabe], await Search("12,50"));
        Assert.Equal([einnahme, ausgabe], await Search("12.5"));
    }

    [Fact]
    public async Task Vorzeichen_schraenkt_ein()
    {
        var ausgabe = Add("Musterhandel GmbH", -12.50m);
        var einnahme = Add("Erika Mustermann", 12.50m);

        Assert.Equal([ausgabe], await Search("-12,50"));
        Assert.Equal([einnahme], await Search("+12,50"));
    }

    [Fact]
    public async Task Findet_Tausenderbetraege()
    {
        var miete = Add("Hausverwaltung", -1234.56m);

        Assert.Equal([miete], await Search("1.234,56"));
    }

    [Fact]
    public async Task Findet_aufgeteilte_Buchung_ueber_Gesamt_und_Teilbetrag()
    {
        var lebensmittel = AddCategory("Lebensmittel");
        var werkzeug = AddCategory("Werkzeug");
        var id = Add("Kaufland", -80m);
        var transaction = await _context.Transactions.Include(t => t.Splits).SingleAsync(t => t.Id == id);
        transaction.Split([new TransactionSplitDto(lebensmittel, -30.01m), new TransactionSplitDto(werkzeug, -49.99m)]);
        await _context.SaveChangesAsync();

        Assert.Equal([id], await Search("80"));
        Assert.Equal([id], await Search("49,99"));
        Assert.Empty(await Search("50"));
    }

    [Fact]
    public async Task Textsuche_bleibt_daneben_bestehen()
    {
        var rechnung = Add("Musterhandel GmbH", -5m, verwendungszweck: "RE 2026");
        var betrag = Add("Stadtwerke", -2026m);

        Assert.Equal([betrag, rechnung], await Search("2026"));
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

    private int AddCategory(string name)
    {
        var category = new Category { AccountId = _account.Id, Name = name };
        _context.Categories.Add(category);
        _context.SaveChanges();
        return category.Id;
    }

    private async Task<int[]> Search(string search)
    {
        _context.ChangeTracker.Clear();
        var controller = new TransactionsController(_context, null!, TimeProvider.System)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = _services },
            },
        };
        var page = await controller.GetTransactions(_account.Id, new TransactionQuery(null, null, search, null), CancellationToken.None);
        return page.Items.Select(t => t.Id).ToArray();
    }
}
