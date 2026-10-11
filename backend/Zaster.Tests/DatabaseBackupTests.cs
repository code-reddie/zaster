using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Zaster.Accounts;
using Zaster.Backup;
using Zaster.Database;
using Zaster.Users;

namespace Zaster.Tests;

public sealed class DatabaseBackupTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _context;
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"zaster-test-{Guid.NewGuid():N}.db");

    public DatabaseBackupTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    [Fact]
    public async Task OnlyTheFirstUserMayDownload()
    {
        var anna = AddUser("Anna");
        var ben = AddUser("Ben");

        Assert.True(await DatabaseBackup.IsAllowedAsync(_context, anna.Id, CancellationToken.None));
        Assert.False(await DatabaseBackup.IsAllowedAsync(_context, ben.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CopyContainsAllData()
    {
        var anna = AddUser("Anna");
        _context.Accounts.Add(new Account { Name = "Girokonto", Iban = "DE02120300000000202051", Users = [anna] });
        _context.SaveChanges();

        await DatabaseBackup.WriteAsync(_context, _path, CancellationToken.None);

        using var copy = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_path};Mode=ReadOnly;Pooling=False").Options);
        var account = await copy.Accounts.Include(a => a.Users).SingleAsync();
        Assert.Equal("Girokonto", account.Name);
        Assert.Equal("Anna", Assert.Single(account.Users).Name);
    }

    private User AddUser(string name)
    {
        var user = new User { Name = name, PasswordHash = "x" };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }
}
