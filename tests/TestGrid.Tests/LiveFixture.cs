using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using TestGrid.Changes;
using TestGrid.Data;
using TestGrid.ViewModels;

namespace TestGrid.Tests;

public sealed class LiveFixture : IDisposable
{
    private readonly ModifiedOnDescendingIdAscendingSort sort = new();
    private int newestSecond = 1000;

    public LiveFixture(int pageSize = 20)
    {
        Time = new FakeTimeProvider();
        Grid = new GridViewModel(Factory, Time, new ImmediateUiDispatcher())
        {
            PageSize = pageSize,
            Filter = item => item.Quantity > 10,
        };
    }

    public TestDbContextFactory Factory { get; } = new();

    public FakeTimeProvider Time { get; }

    public GridViewModel Grid { get; }

    public async Task<List<Guid>> SeedAsync(int count)
    {
        await using var context = Factory.CreateDbContext();
        context.Items.AddRange(Enumerable.Range(0, count).Select(index => new Item
        {
            Name = $"pc part {index}",
            Quantity = 20,
            ModifiedOn = TestData.BaseModifiedOn.AddSeconds(index),
        }));
        await context.SaveChangesAsync();
        return await ExpectedOrderAsync();
    }

    public async Task<Guid> AddNewestAsync(int quantity = 20, string name = "pc part new")
    {
        var item = new Item
        {
            Name = name,
            Quantity = quantity,
            ModifiedOn = TestData.BaseModifiedOn.AddSeconds(++newestSecond),
        };
        await using var context = Factory.CreateDbContext();
        context.Items.Add(item);
        await context.SaveChangesAsync();
        return item.Id;
    }

    public async Task<Guid> AddAtAsync(DateTimeOffset modifiedOn, int quantity = 20)
    {
        var item = new Item { Name = "pc part inserted", Quantity = quantity, ModifiedOn = modifiedOn };
        await using var context = Factory.CreateDbContext();
        context.Items.Add(item);
        await context.SaveChangesAsync();
        return item.Id;
    }

    public async Task UpdateAsync(Guid id, Action<Item> change)
    {
        await using var context = Factory.CreateDbContext();
        var item = await context.Items.SingleAsync(i => i.Id == id);
        change(item);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var context = Factory.CreateDbContext();
        await context.Items.Where(i => i.Id == id).ExecuteDeleteAsync();
    }

    public int NextNewestSecond() => ++newestSecond;

    public Task ApplyAsync(
        IEnumerable<Guid>? inserted = null,
        IEnumerable<Guid>? updated = null,
        IEnumerable<Guid>? deleted = null)
    {
        var batch = new ChangeBatch(
            1,
            (inserted ?? []).ToHashSet(),
            (updated ?? []).ToHashSet(),
            (deleted ?? []).ToHashSet());
        return Grid.ApplyChangesAsync(batch);
    }

    public async Task<List<Guid>> ExpectedOrderAsync()
    {
        await using var context = Factory.CreateDbContext();
        return await sort.Order(context.Items.Where(item => item.Quantity > 10))
            .Select(item => item.Id)
            .ToListAsync();
    }

    public async Task AssertWindowMatchesDatabaseAsync()
    {
        var expected = await ExpectedOrderAsync();
        var ids = Grid.Items.Select(item => item.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        if (ids.Count == 0)
        {
            Assert.Empty(expected);
            return;
        }

        var start = expected.IndexOf(ids[0]);
        Assert.True(start >= 0, "first loaded row is not in the database result");
        Assert.Equal(expected.Skip(start).Take(ids.Count), ids);
        if (!Grid.HasMoreBefore && !Grid.IsHeadFrozen)
        {
            Assert.Equal(0, start);
        }

        Assert.Equal(start + ids.Count < expected.Count, Grid.HasMoreAfter);
    }

    public void Dispose() => Factory.Dispose();
}
