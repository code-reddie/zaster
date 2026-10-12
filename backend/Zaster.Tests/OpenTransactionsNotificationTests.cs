using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Zaster.Accounts;
using Zaster.Categories;
using Zaster.Database;
using Zaster.Notifications;
using Zaster.Transactions;

namespace Zaster.Tests;

public sealed class OpenTransactionsNotificationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _context;
    private readonly OpenTransactionsQueue _queue = new();
    private readonly TransactionImporter _importer;
    private readonly Account _account;

    public OpenTransactionsNotificationTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _account = new Account { Name = "Girokonto", Iban = "DE02120300000000202051" };
        _context.Accounts.Add(_account);
        _context.SaveChanges();

        var category = new Category { AccountId = _account.Id, Name = "Lebensmittel" };
        _context.Categories.Add(category);
        _context.SaveChanges();
        _context.Rules.Add(new Rule { AccountId = _account.Id, Conditions = [new RuleCondition { Field = RuleField.Auftraggeber, Pattern = "Kaufland" }], CategoryId = category.Id });
        _context.SaveChanges();

        _importer = new TransactionImporter(_context, TimeProvider.System, _queue);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Meldet_nur_Buchungen_die_nach_den_Regeln_ohne_Kategorie_bleiben()
    {
        await ImportAsync(Item("Kaufland Musterstadt", -23.45m), Item("Stadtwerke Beispiel", -80m), Item("Bäckerei Probe", -4.2m));

        Assert.True(_queue.Reader.TryRead(out var notification));
        Assert.Equal(new OpenTransactionsNotification(_account.Id, 2), notification);
        Assert.False(_queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Keine_Meldung_wenn_Regeln_alles_zuordnen()
    {
        await ImportAsync(Item("Kaufland Musterstadt", -23.45m));

        Assert.False(_queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Keine_Meldung_fuer_schon_vorhandene_Buchungen()
    {
        await ImportAsync(Item("Stadtwerke Beispiel", -80m));
        Assert.True(_queue.Reader.TryRead(out _));

        // Der nächste Abruf überlappt mit dem vorigen und bringt dieselbe Buchung noch einmal.
        await ImportAsync(Item("Stadtwerke Beispiel", -80m));

        Assert.False(_queue.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData(1, 1, "1 neue Buchung ohne Kategorie")]
    [InlineData(3, 3, "3 neue Buchungen ohne Kategorie")]
    [InlineData(3, 12, "3 neue Buchungen ohne Kategorie (insgesamt 12 offen)")]
    public void Text_nennt_neue_und_alle_offenen(int newOpen, int totalOpen, string expected) =>
        Assert.Equal(expected, OpenTransactionsNotifier.Message(newOpen, totalOpen));

    [Theory]
    [InlineData("https://zaster.example.org", "https://zaster.example.org/konten/7/offen")]
    [InlineData("https://example.org/zaster", "https://example.org/zaster/konten/7/offen")]
    public void Link_fuehrt_zur_Seite_ohne_Kategorie(string zasterUrl, string expected)
    {
        Assert.True(HomeAssistantClient.TryParseHttpUri(zasterUrl, out var uri));
        Assert.Equal(expected, OpenTransactionsNotifier.Link(uri, 7).AbsoluteUri);
    }

    private Task ImportAsync(params ImportTransaction[] items) =>
        _importer.ImportAsync(_account.Id, new List<ImportTransaction>(items), TransactionSource.FinTs, CancellationToken.None);

    private static ImportTransaction Item(string auftraggeber, decimal betrag) =>
        new(new DateOnly(2026, 10, 9), null, auftraggeber, "Lastschrift", null, betrag);
}
