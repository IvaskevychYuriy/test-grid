using Microsoft.EntityFrameworkCore;
using TestGrid.Data;
using TestGrid.ViewModels;

namespace TestGrid.Tests;

public class FilterSortTests
{
    private static readonly Guid LowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MiddleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid HighId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    [Fact]
    public async Task ItemsPageAppliesFilterOnlyToMatchingRows()
    {
        using var factory = new TestDbContextFactory();
        await new DbSeeder(factory).SeedIfEmptyAsync();
        var page = new ItemsViewModel(new GridViewModel(factory));

        await page.Grid.LoadAsync();

        Assert.NotEmpty(page.Grid.Items);
        Assert.All(page.Grid.Items, item =>
        {
            Assert.True(item.Quantity > PcPartCriteria.QuantityThreshold);
            Assert.StartsWith(PcPartCriteria.NamePrefix, item.Name);
        });
    }

    [Fact]
    public async Task ExcludesRowsAtQuantityBoundaryAndWithOtherNames()
    {
        using var factory = new TestDbContextFactory();
        await using (var context = factory.CreateDbContext())
        {
            context.Items.AddRange(
                new Item { Name = "pc part ok", Quantity = 11 },
                new Item { Name = "pc part boundary", Quantity = 10 },
                new Item { Name = "other", Quantity = 100 });
            await context.SaveChangesAsync();
        }

        var page = new ItemsViewModel(new GridViewModel(factory));
        await page.Grid.LoadAsync();

        Assert.Equal(["pc part ok"], page.Grid.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task SortsByModifiedOnDescendingThenIdAscending()
    {
        using var factory = new TestDbContextFactory();
        await using (var context = factory.CreateDbContext())
        {
            context.Items.AddRange(
                new Item { Id = HighId, Name = "pc part c", Quantity = 20 },
                new Item { Id = LowId, Name = "pc part a", Quantity = 20 },
                new Item { Id = MiddleId, Name = "pc part b", Quantity = 20 });
            await context.SaveChangesAsync();
        }

        var page = new ItemsViewModel(new GridViewModel(factory));
        await page.Grid.LoadAsync();
        Assert.Equal([LowId, MiddleId, HighId], page.Grid.Items.Select(item => item.Id));

        await Task.Delay(20);
        await using (var context = factory.CreateDbContext())
        {
            var item = await context.Items.SingleAsync(i => i.Id == HighId);
            item.Quantity = 30;
            await context.SaveChangesAsync();
        }

        await page.Grid.LoadAsync();
        Assert.Equal([HighId, LowId, MiddleId], page.Grid.Items.Select(item => item.Id));
    }

    [Fact]
    public void SeederGeneratesHalfMatchingRows()
    {
        var items = DbSeeder.GenerateItems(DbSeeder.SeedRowCount);

        var matching = items.Count(item =>
            item.Quantity > PcPartCriteria.QuantityThreshold
            && item.Name.StartsWith(PcPartCriteria.NamePrefix));

        Assert.Equal(DbSeeder.SeedRowCount / 2, matching);
    }
}
