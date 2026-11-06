using Bogus;
using Microsoft.EntityFrameworkCore;
using TestGrid.Data;

namespace TestGrid.Changes;

public class DataChangeGenerator(
    IDbContextFactory<AppDbContext> contextFactory,
    IDataChangeNotifier notifier,
    DataChangeOptions options)
{
    private readonly Faker faker = new();

    public async Task ApplyChangesAsync(CancellationToken cancellationToken = default)
    {
        var count = options.ChangesPerType;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await context.Items
            .OrderBy(_ => EF.Functions.Random())
            .Take(count * 2)
            .ToListAsync(cancellationToken);
        var toDelete = existing.Skip(count).ToList();
        var updates = existing.Take(count).Select(item => (Item: item, Patch: ChangeRandomFields(item))).ToList();
        var toInsert = DbSeeder.GenerateItems(count, modifiedOn: DateTimeOffset.UtcNow);

        context.Items.RemoveRange(toDelete);
        context.Items.AddRange(toInsert);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var item in toInsert)
        {
            Publish(DataChangeType.Insert, item.Id, new ItemPatch(item.Name, item.Quantity, item.Price, item.ModifiedOn));
        }

        foreach (var (item, patch) in updates)
        {
            Publish(DataChangeType.Update, item.Id, patch);
        }

        foreach (var item in toDelete)
        {
            Publish(DataChangeType.Delete, item.Id, null);
        }
    }

    private void Publish(DataChangeType changeType, Guid itemId, ItemPatch? patch) =>
        notifier.Publish(new DataChangedEventArgs(changeType, itemId, patch));

    private ItemPatch ChangeRandomFields(Item item)
    {
        var patch = new ItemPatch(
            Name: faker.Random.Bool(0.3f) ? faker.Commerce.ProductName() : null,
            Quantity: faker.Random.Bool() ? faker.Random.Int(0, 500) : null,
            Price: faker.Random.Bool() ? faker.Random.Float(1, 2000) : null,
            ModifiedOn: faker.Random.Bool() ? DateTimeOffset.UtcNow : null);

        if (patch is { Name: null, Quantity: null, Price: null })
        {
            patch = patch with { Quantity = faker.Random.Int(0, 500) };
        }

        item.Name = patch.Name ?? item.Name;
        item.Quantity = patch.Quantity ?? item.Quantity;
        item.Price = patch.Price ?? item.Price;
        item.ModifiedOn = patch.ModifiedOn ?? item.ModifiedOn;
        return patch;
    }
}
