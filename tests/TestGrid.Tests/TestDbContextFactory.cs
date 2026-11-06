using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestGrid.Data;

namespace TestGrid.Tests;

public sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public TestDbContextFactory()
    {
        connection.Open();
        using var context = CreateDbContext();
        context.Database.Migrate();
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose() => connection.Dispose();
}
