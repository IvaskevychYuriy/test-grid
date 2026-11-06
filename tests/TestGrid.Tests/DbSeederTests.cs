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
    public void GeneratedRowsHaveUtcModifiedOnSpreadOverRecentDays()
    {
        var now = DateTimeOffset.UtcNow;

        var items = DbSeeder.GenerateItems(200);

        Assert.All(items, item =>
        {
            Assert.Equal(TimeSpan.Zero, item.ModifiedOn.Offset);
            Assert.InRange(item.ModifiedOn, now.AddDays(-31), now.AddMinutes(1));
        });
        Assert.True(items.Select(item => item.ModifiedOn).Distinct().Count() > 100);
    }

    [Fact]
    public void ExplicitModifiedOnIsAppliedToAllGeneratedRows()
    {
        var stamp = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        var items = DbSeeder.GenerateItems(10, stamp);

        Assert.All(items, item => Assert.Equal(stamp, item.ModifiedOn));
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
