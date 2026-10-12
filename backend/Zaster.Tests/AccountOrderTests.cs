using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zaster.Accounts;
using Zaster.Database;
using Zaster.Users;

namespace Zaster.Tests;

public sealed class AccountOrderTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
    private readonly AppDbContext _context;
    private readonly User _anna;
    private readonly User _ben;
    private readonly Account _giro;
    private readonly Account _gemeinsam;
    private readonly Account _tagesgeld;

    public AccountOrderTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _anna = new User { Name = "Anna", PasswordHash = "-" };
        _ben = new User { Name = "Ben", PasswordHash = "-" };
        _context.Users.AddRange(_anna, _ben);
        _giro = new Account { Name = "Giro", Iban = "DE02120300000000202051", Users = [_anna] };
        _gemeinsam = new Account { Name = "Gemeinsam", Iban = "DE02500105170137075030", Users = [_anna, _ben] };
        _tagesgeld = new Account { Name = "Tagesgeld", Iban = "DE89370400440532013000", Users = [_anna] };
        _context.Accounts.AddRange(_giro, _gemeinsam, _tagesgeld);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Ohne_eigene_Reihenfolge_nach_Name()
    {
        Assert.Equal(["Gemeinsam", "Giro", "Tagesgeld"], await NamesFor(_anna));
    }

    [Fact]
    public async Task Gespeicherte_Reihenfolge_gilt_beim_Laden()
    {
        var result = await ControllerFor(_anna).Reorder(new ReorderAccounts([_tagesgeld.Id, _giro.Id, _gemeinsam.Id]), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(["Tagesgeld", "Giro", "Gemeinsam"], await NamesFor(_anna));
    }

    [Fact]
    public async Task Reihenfolge_gilt_nur_fuer_den_eigenen_Nutzer()
    {
        await ControllerFor(_anna).Reorder(new ReorderAccounts([_tagesgeld.Id, _giro.Id, _gemeinsam.Id]), CancellationToken.None);
        var ben = new Account { Name = "Bens Konto", Iban = "DE75512108001245126199", Users = [await _context.Users.SingleAsync(u => u.Id == _ben.Id)] };
        _context.Accounts.Add(ben);
        await _context.SaveChangesAsync();

        await ControllerFor(_ben).Reorder(new ReorderAccounts([_gemeinsam.Id, ben.Id]), CancellationToken.None);

        Assert.Equal(["Gemeinsam", "Bens Konto"], await NamesFor(_ben));
        Assert.Equal(["Tagesgeld", "Giro", "Gemeinsam"], await NamesFor(_anna));
    }

    [Fact]
    public async Task Neue_Konten_stehen_hinten()
    {
        await ControllerFor(_anna).Reorder(new ReorderAccounts([_tagesgeld.Id, _giro.Id, _gemeinsam.Id]), CancellationToken.None);
        var anna = await _context.Users.SingleAsync(u => u.Id == _anna.Id);
        _context.Accounts.Add(new Account { Name = "Aktien", Iban = "DE75512108001245126199", Users = [anna] });
        await _context.SaveChangesAsync();

        Assert.Equal(["Tagesgeld", "Giro", "Gemeinsam", "Aktien"], await NamesFor(_anna));
    }

    [Fact]
    public void Konten_ohne_Platz_behalten_ihre_Reihenfolge_dahinter()
    {
        AccountDto Dto(int id) => new(id, $"Konto {id}", "", null, false, [], null, null, [], 0);

        var sorted = AccountsController.Sort([Dto(1), Dto(2), Dto(3), Dto(4)], [3, 99, 1]);

        Assert.Equal([3, 1, 2, 4], sorted.Select(a => a.Id));
    }

    [Theory]
    [InlineData(false, false)] // ein Konto fehlt
    [InlineData(true, true)]   // ein fremdes Konto dabei
    [InlineData(true, false)]  // ein Konto doppelt
    public async Task Lehnt_Listen_ab_die_nicht_zu_den_eigenen_Konten_passen(bool complete, bool foreign)
    {
        var ben = new Account { Name = "Bens Konto", Iban = "DE75512108001245126199", Users = [_ben] };
        _context.Accounts.Add(ben);
        await _context.SaveChangesAsync();
        int[] ids = complete ? [_giro.Id, _gemeinsam.Id, _tagesgeld.Id] : [_giro.Id, _gemeinsam.Id];
        ids = foreign ? [.. ids, ben.Id] : complete ? [.. ids, _giro.Id] : ids;

        var result = await ControllerFor(_anna).Reorder(new ReorderAccounts([.. ids]), CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(["Gemeinsam", "Giro", "Tagesgeld"], await NamesFor(_anna));
    }

    private async Task<string[]> NamesFor(User user) =>
        (await ControllerFor(user).GetAccounts(CancellationToken.None)).Select(a => a.Name).ToArray();

    private AccountsController ControllerFor(User user)
    {
        _context.ChangeTracker.Clear();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "Test"));
        return new AccountsController(_context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal, RequestServices = _services },
            },
        };
    }
}
