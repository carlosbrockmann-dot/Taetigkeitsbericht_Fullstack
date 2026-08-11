using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Taetigkeitsbericht.Backend.Data;
using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Tests.Integration;

/// <summary>In-Memory-SQLite für Repository-/DbContext-Tests ohne PostgreSQL.</summary>
public sealed class SqliteAppDbFixture : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection;

    public AppDbContext Db { get; }

    public SqliteAppDbFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();
    }

    public async Task InitializeAsync()
    {
        var mitarbeiter = new Mitarbeiter
        {
            Benutzername = "testuser",
            Email = "test@example.com",
            PasswortHash = "hash",
            EmailBestaetigt = true,
        };
        Db.Mitarbeiter.Add(mitarbeiter);
        await Db.SaveChangesAsync();
    }

    public int MitarbeiterId => Db.Mitarbeiter.Single().Id;

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
