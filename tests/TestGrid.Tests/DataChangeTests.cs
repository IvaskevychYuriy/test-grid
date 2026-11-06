using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TestGrid.Changes;

namespace TestGrid.Tests;

public class DataChangeTests
{
    [Fact]
    public async Task OneTickAppliesConfiguredChangesAndPublishesEvents()
    {
        using var factory = new TestDbContextFactory();
        await TestData.AddItemsAsync(factory, 20);
        var before = await ReadItemsAsync(factory);
        var tickStart = DateTimeOffset.UtcNow;
        var (generator, events) = CreateGenerator(factory, new DataChangeOptions());

        await generator.ApplyChangesAsync(TestContext.Current.CancellationToken);

        var inserts = events.Where(e => e.ChangeType == DataChangeType.Insert).ToList();
        var updates = events.Where(e => e.ChangeType == DataChangeType.Update).ToList();
        var deletes = events.Where(e => e.ChangeType == DataChangeType.Delete).ToList();
        Assert.Equal(4, inserts.Count);
        Assert.Equal(4, updates.Count);
        Assert.Equal(4, deletes.Count);

        var after = await ReadItemsAsync(factory);
        Assert.Equal(20, after.Count);
        Assert.All(deletes, e =>
        {
            Assert.Null(e.Patch);
            Assert.False(after.ContainsKey(e.ItemId));
            Assert.True(before.ContainsKey(e.ItemId));
        });
        Assert.All(inserts, e =>
        {
            Assert.False(before.ContainsKey(e.ItemId));
            var item = after[e.ItemId];
            Assert.Equal(new ItemPatch(item.Name, item.Quantity, item.Price, item.ModifiedOn), e.Patch);
            Assert.True(item.ModifiedOn >= tickStart);
            Assert.Equal(TimeSpan.Zero, item.ModifiedOn.Offset);
        });
        Assert.All(updates, e =>
        {
            var item = after[e.ItemId];
            var patch = e.Patch!;
            Assert.Equal(patch.ModifiedOn ?? before[e.ItemId].ModifiedOn, item.ModifiedOn);
            Assert.Equal(patch.Name ?? before[e.ItemId].Name, item.Name);
            Assert.Equal(patch.Quantity ?? before[e.ItemId].Quantity, item.Quantity);
            Assert.Equal(patch.Price ?? before[e.ItemId].Price, item.Price);
            Assert.True(patch.Name is not null || patch.Quantity is not null || patch.Price is not null);
        });
    }

    [Fact]
    public async Task UpdatesRandomlyStampOrKeepModifiedOn()
    {
        using var factory = new TestDbContextFactory();
        await TestData.AddItemsAsync(factory, 50);
        var (generator, events) = CreateGenerator(factory, new DataChangeOptions());

        for (var tick = 0; tick < 10; tick++)
        {
            await generator.ApplyChangesAsync(TestContext.Current.CancellationToken);
        }

        var updates = events.Where(e => e.ChangeType == DataChangeType.Update).ToList();
        Assert.Contains(updates, e => e.Patch!.ModifiedOn is not null);
        Assert.Contains(updates, e => e.Patch!.ModifiedOn is null);
        Assert.All(updates, e => Assert.True(
            e.Patch!.Name is not null || e.Patch.Quantity is not null || e.Patch.Price is not null));
    }

    [Fact]
    public async Task ChangesPerTypeControlsEveryChangeCount()
    {
        using var factory = new TestDbContextFactory();
        await TestData.AddItemsAsync(factory, 20);
        var (generator, events) = CreateGenerator(factory, new DataChangeOptions { ChangesPerType = 2 });

        await generator.ApplyChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, events.Count(e => e.ChangeType == DataChangeType.Insert));
        Assert.Equal(2, events.Count(e => e.ChangeType == DataChangeType.Update));
        Assert.Equal(2, events.Count(e => e.ChangeType == DataChangeType.Delete));
    }

    [Fact]
    public async Task HandlesFewerRowsThanRequestedChanges()
    {
        using var factory = new TestDbContextFactory();
        await TestData.AddItemsAsync(factory, 3);
        var (generator, events) = CreateGenerator(factory, new DataChangeOptions());

        await generator.ApplyChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, events.Count(e => e.ChangeType == DataChangeType.Insert));
        Assert.Equal(3, events.Count(e => e.ChangeType == DataChangeType.Update));
        Assert.DoesNotContain(events, e => e.ChangeType == DataChangeType.Delete);
        Assert.Equal(7, (await ReadItemsAsync(factory)).Count);
    }

    [Fact]
    public async Task OnlyInsertsIntoEmptyDatabase()
    {
        using var factory = new TestDbContextFactory();
        var (generator, events) = CreateGenerator(factory, new DataChangeOptions());

        await generator.ApplyChangesAsync(TestContext.Current.CancellationToken);

        Assert.All(events, e => Assert.Equal(DataChangeType.Insert, e.ChangeType));
        Assert.Equal(4, events.Count);
    }

    [Fact]
    public async Task BackgroundServiceAppliesChangesPeriodically()
    {
        using var factory = new TestDbContextFactory();
        await TestData.AddItemsAsync(factory, 20);
        var options = new DataChangeOptions { Interval = TimeSpan.FromMilliseconds(50) };
        var (generator, events) = CreateGenerator(factory, options);
        var service = new BackgroundDataChanger(generator, options, NullLogger<BackgroundDataChanger>.Instance);

        await service.StartAsync(TestContext.Current.CancellationToken);
        for (var wait = 0; wait < 100 && events.Count < 24; wait++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(events.Count >= 24);
    }

    private static (DataChangeGenerator Generator, List<DataChangedEventArgs> Events) CreateGenerator(
        TestDbContextFactory factory,
        DataChangeOptions options)
    {
        var notifier = new DataChangeNotifier();
        var events = new List<DataChangedEventArgs>();
        notifier.DataChanged += (_, e) =>
        {
            lock (events)
            {
                events.Add(e);
            }
        };

        return (new DataChangeGenerator(factory, notifier, options), events);
    }

    private static async Task<Dictionary<Guid, TestGrid.Data.Item>> ReadItemsAsync(TestDbContextFactory factory)
    {
        await using var context = factory.CreateDbContext();
        return await context.Items.AsNoTracking().ToDictionaryAsync(item => item.Id);
    }
}
