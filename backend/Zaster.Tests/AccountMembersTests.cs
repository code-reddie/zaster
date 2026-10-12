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

public sealed class AccountMembersTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
    private readonly AppDbContext _context;
    private readonly User _anna;
    private readonly User _ben;
    private readonly User _clara;
    private readonly Account _account;

    public AccountMembersTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _anna = new User { Name = "Anna", PasswordHash = "-" };
        _ben = new User { Name = "Ben", PasswordHash = "-" };
        _clara = new User { Name = "Clara", PasswordHash = "-" };
        _context.Users.AddRange(_anna, _ben, _clara);
        _account = new Account { Name = "Gemeinschaftskonto", Iban = "DE02120300000000202051", Users = [_anna] };
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
    public async Task Hinzugefuegte_Person_ist_sofort_Mitglied()
    {
        var result = await ControllerFor(_anna).AddMember(_account.Id, new AddMember("  Ben "), CancellationToken.None);

        Assert.Equal(["Anna", "Ben"], result.Value!.Members.Select(m => m.Name));
        Assert.Contains(_account.Id, await AccountIdsOf(_ben));
    }

    [Fact]
    public async Task Jedes_Mitglied_kann_weitere_Personen_hinzufuegen()
    {
        await ControllerFor(_anna).AddMember(_account.Id, new AddMember("Ben"), CancellationToken.None);

        var result = await ControllerFor(_ben).AddMember(_account.Id, new AddMember("Clara"), CancellationToken.None);

        Assert.Equal(["Anna", "Ben", "Clara"], result.Value!.Members.Select(m => m.Name));
    }

    [Theory]
    [InlineData("Unbekannt", StatusCodes.Status400BadRequest)]
    [InlineData("ben", StatusCodes.Status400BadRequest)]
    [InlineData(" ", StatusCodes.Status400BadRequest)]
    [InlineData("Anna", StatusCodes.Status409Conflict)]
    public async Task Lehnt_unbekannte_Namen_und_vorhandene_Mitglieder_ab(string name, int status)
    {
        var result = await ControllerFor(_anna).AddMember(_account.Id, new AddMember(name), CancellationToken.None);

        Assert.Equal(status, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(["Anna"], await MemberNames());
    }

    [Fact]
    public async Task Mitglied_kann_andere_entfernen()
    {
        await ControllerFor(_anna).AddMember(_account.Id, new AddMember("Ben"), CancellationToken.None);

        var result = await ControllerFor(_ben).RemoveMember(_account.Id, _anna.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(["Ben"], await MemberNames());
        Assert.Empty(await AccountIdsOf(_anna));
    }

    [Fact]
    public async Task Mitglied_kann_das_Konto_verlassen()
    {
        await ControllerFor(_anna).AddMember(_account.Id, new AddMember("Ben"), CancellationToken.None);

        var result = await ControllerFor(_ben).RemoveMember(_account.Id, _ben.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(["Anna"], await MemberNames());
    }

    [Fact]
    public async Task Letztes_Mitglied_kann_nicht_gehen()
    {
        var result = await ControllerFor(_anna).RemoveMember(_account.Id, _anna.Id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(["Anna"], await MemberNames());
    }

    [Fact]
    public async Task Entfernen_eines_Nichtmitglieds_liefert_404()
    {
        var result = await ControllerFor(_anna).RemoveMember(_account.Id, _clara.Id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result).StatusCode);
    }

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

    private async Task<string[]> MemberNames() =>
        await _context.Accounts
            .Where(a => a.Id == _account.Id)
            .SelectMany(a => a.Users)
            .OrderBy(u => u.Name)
            .Select(u => u.Name)
            .ToArrayAsync();

    private async Task<int[]> AccountIdsOf(User user) =>
        await _context.Users
            .Where(u => u.Id == user.Id)
            .SelectMany(u => u.Accounts)
            .Select(a => a.Id)
            .ToArrayAsync();
}
