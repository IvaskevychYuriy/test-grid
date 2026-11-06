using Microsoft.EntityFrameworkCore;
using TestGrid.Data;

namespace TestGrid.Tests;

public class DbSeederTests
{
    [Fact]
    public async Task SeedsThousandRowsIntoEmptyDatabase()
    {
        using var factory = new TestDbContextFactory();

        await new DbSeeder(factory).SeedIfEmptyAsync();

        await using var context = factory.CreateDbContext();
        Assert.Equal(DbSeeder.SeedRowCount, await context.Items.CountAsync());
    }

    [Fact]
    public async Task DoesNotSeedNonEmptyDatabase()
    {
        using var factory = new TestDbContextFactory();
        await using (var context = factory.CreateDbContext())
        {
            context.Items.Add(new Item { Name = "existing" });
            await context.SaveChangesAsync();
        }

        await new DbSeeder(factory).SeedIfEmptyAsync();

        await using var readContext = factory.CreateDbContext();
        Assert.Equal(1, await readContext.Items.CountAsync());
    }
}
