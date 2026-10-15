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

public sealed class RueckgaengigTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
    private readonly AppDbContext _context;
    private readonly Account _account;
    private readonly Category _lebensmittel;
    private readonly Category _werkzeug;
    private readonly Transaction _einkauf;
    private readonly Transaction _miete;

    public RueckgaengigTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _account = new Account { Name = "Girokonto", Iban = "DE02120300000000202051" };
        _context.Accounts.Add(_account);
        _context.SaveChanges();

        _lebensmittel = new Category { AccountId = _account.Id, Name = "Lebensmittel" };
        _werkzeug = new Category { AccountId = _account.Id, Name = "Werkzeug" };
        _context.Categories.AddRange(_lebensmittel, _werkzeug);

        _einkauf = NewTransaction("Supermarkt Muster", -80m);
        _miete = NewTransaction("Hausverwaltung Beispiel", -700m);
        _context.Transactions.AddRange(_einkauf, _miete);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Kategorie_zuweisen_laesst_sich_rueckgaengig_machen()
    {
        await Controller().SetBemerkung(_account.Id, _einkauf.Id, new SetBemerkung("Wocheneinkauf"), CancellationToken.None);
        var before = await StateOf(_einkauf.Id);
        await Controller().SetCategory(_account.Id, _einkauf.Id, new SetCategory(_lebensmittel.Id), CancellationToken.None);

        var result = await Controller().Restore(_account.Id, new RestoreTransactions([before]), CancellationToken.None);

        var restored = Assert.Single(result.Value!);
        Assert.Empty(restored.Splits);
        Assert.False(restored.Reviewed);
        Assert.Equal("Wocheneinkauf", restored.Bemerkung);
        Assert.Equal(before, await StateOf(_einkauf.Id));
    }

    [Fact]
    public async Task Aufteilung_kommt_zurueck()
    {
        await Controller().SetSplits(
            _account.Id,
            _einkauf.Id,
            new SetSplits([new TransactionSplitDto(_lebensmittel.Id, -50m), new TransactionSplitDto(_werkzeug.Id, -30m)]),
            CancellationToken.None);
        var before = await StateOf(_einkauf.Id);
        await Controller().SetCategory(_account.Id, _einkauf.Id, new SetCategory(_werkzeug.Id), CancellationToken.None);

        await Controller().Restore(_account.Id, new RestoreTransactions([before]), CancellationToken.None);

        var after = await StateOf(_einkauf.Id);
        Assert.True(after.Reviewed);
        Assert.Equal(
            before.Splits.OrderBy(s => s.CategoryId),
            after.Splits.OrderBy(s => s.CategoryId));
    }

    [Fact]
    public async Task Nicht_kategorisieren_wird_wieder_offen()
    {
        var before = await StateOf(_einkauf.Id);
        await Controller().SetReviewed(_account.Id, _einkauf.Id, new SetReviewed(true), CancellationToken.None);

        await Controller().Restore(_account.Id, new RestoreTransactions([before]), CancellationToken.None);

        Assert.False((await StateOf(_einkauf.Id)).Reviewed);
    }

    [Fact]
    public async Task Alle_nicht_kategorisieren_liefert_den_Zustand_vorher()
    {
        await Controller().SetBemerkung(_account.Id, _miete.Id, new SetBemerkung("Oktober"), CancellationToken.None);
        await Controller().SetCategory(_account.Id, _einkauf.Id, new SetCategory(_lebensmittel.Id), CancellationToken.None);

        var review = await Controller().Review(_account.Id, new ReviewTransactions(null), CancellationToken.None);

        var previous = Assert.Single(review.Previous);
        Assert.Equal(1, review.Updated);
        Assert.Equal(_miete.Id, previous.Id);
        Assert.Empty(previous.Splits);
        Assert.False(previous.Reviewed);
        Assert.Equal("Oktober", previous.Bemerkung);
        Assert.True((await StateOf(_miete.Id)).Reviewed);

        await Controller().Restore(_account.Id, new RestoreTransactions(review.Previous), CancellationToken.None);

        var after = await StateOf(_miete.Id);
        Assert.False(after.Reviewed);
        Assert.Equal("Oktober", after.Bemerkung);
        Assert.True((await StateOf(_einkauf.Id)).Reviewed);
    }

    [Fact]
    public async Task Regeln_anwenden_liefert_nur_die_zugeordneten_Buchungen()
    {
        await Controller().SetReviewed(_account.Id, _einkauf.Id, new SetReviewed(true), CancellationToken.None);
        _context.Rules.Add(new Rule
        {
            AccountId = _account.Id,
            CategoryId = _lebensmittel.Id,
            Conditions = [new RuleCondition { Field = RuleField.Auftraggeber, Pattern = "supermarkt" }],
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var apply = await new RulesController(_context).ApplyRules(_account.Id, CancellationToken.None);

        var previous = Assert.Single(apply.Previous);
        Assert.Equal(_einkauf.Id, previous.Id);
        Assert.Empty(previous.Splits);
        Assert.True(previous.Reviewed);

        await Controller().Restore(_account.Id, new RestoreTransactions(apply.Previous), CancellationToken.None);

        var after = await StateOf(_einkauf.Id);
        Assert.Empty(after.Splits);
        Assert.True(after.Reviewed);
    }

    [Fact]
    public async Task Geloeschte_Buchung_wird_uebersprungen()
    {
        var before = await StateOf(_einkauf.Id);
        await Controller().DeleteTransaction(_account.Id, _einkauf.Id, CancellationToken.None);

        var result = await Controller().Restore(_account.Id, new RestoreTransactions([before]), CancellationToken.None);

        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Geloeschte_Kategorie_wird_abgelehnt()
    {
        await Controller().SetCategory(_account.Id, _einkauf.Id, new SetCategory(_werkzeug.Id), CancellationToken.None);
        var before = await StateOf(_einkauf.Id);
        await Controller().SetCategory(_account.Id, _einkauf.Id, new SetCategory(_lebensmittel.Id), CancellationToken.None);
        await _context.Categories.Where(c => c.Id == _werkzeug.Id).ExecuteDeleteAsync();

        var result = await Controller().Restore(_account.Id, new RestoreTransactions([before]), CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal([_lebensmittel.Id], (await StateOf(_einkauf.Id)).Splits.Select(s => s.CategoryId));
    }

    [Fact]
    public async Task Buchung_eines_anderen_Kontos_bleibt_unberuehrt()
    {
        var other = new Account { Name = "Fremd", Iban = "DE89370400440532013000" };
        _context.Accounts.Add(other);
        await _context.SaveChangesAsync();
        await Controller().SetReviewed(_account.Id, _einkauf.Id, new SetReviewed(true), CancellationToken.None);

        var result = await Controller().Restore(
            other.Id, new RestoreTransactions([new TransactionState(_einkauf.Id, [], false, null)]), CancellationToken.None);

        Assert.Empty(result.Value!);
        Assert.True((await StateOf(_einkauf.Id)).Reviewed);
    }

    private Transaction NewTransaction(string auftraggeber, decimal betrag) => new()
    {
        AccountId = _account.Id,
        Buchung = new DateOnly(2026, 10, 9),
        Valuta = new DateOnly(2026, 10, 9),
        Auftraggeber = auftraggeber,
        Buchungstext = "Lastschrift",
        Betrag = betrag,
        Source = TransactionSource.FinTs,
    };

    private async Task<TransactionState> StateOf(int id)
    {
        _context.ChangeTracker.Clear();
        var state = TransactionState.From(await _context.Transactions.Include(t => t.Splits).SingleAsync(t => t.Id == id));
        // Records vergleichen Listen nach Referenz; für Assert.Equal eine feste Liste ohne Teile.
        return state.Splits.Count == 0 ? state with { Splits = Empty } : state;
    }

    private static readonly TransactionSplitDto[] Empty = [];

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
