using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Tests;

/// <summary>
/// Lekka, niezależna baza SQLite w pamięci do testów usług etapu 1.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<RadarDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new RadarDbContext(options);
        Context.Database.EnsureCreated();
    }

    public RadarDbContext Context { get; }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
