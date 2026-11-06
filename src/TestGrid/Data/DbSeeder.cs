using Bogus;
using Microsoft.EntityFrameworkCore;

namespace TestGrid.Data;

public class DbSeeder(IDbContextFactory<AppDbContext> contextFactory)
{
    public const int SeedRowCount = 1000;

    private const int SeedHistoryDays = 30;

    public async Task SeedIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await context.Items.AnyAsync(cancellationToken))
        {
            return;
        }

        context.Items.AddRange(GenerateItems(SeedRowCount));
        await context.SaveChangesAsync(cancellationToken);
    }

    public static List<Item> GenerateItems(int count, DateTimeOffset? modifiedOn = null)
    {
        var faker = new Faker();
        var items = new List<Item>(count);
        var matchingCount = count / 2;

        for (var index = 0; index < count; index++)
        {
            items.Add(index < matchingCount
                ? CreateMatchingItem(faker)
                : CreateNonMatchingItem(faker, index));
        }

        foreach (var item in items)
        {
            item.ModifiedOn = modifiedOn ?? faker.Date.RecentOffset(SeedHistoryDays).ToUniversalTime();
        }

        return [.. faker.Random.Shuffle(items)];
    }

    private static Item CreateMatchingItem(Faker faker) => new()
    {
        Name = $"{PcPartCriteria.NamePrefix} {faker.Commerce.ProductName()}",
        Quantity = faker.Random.Int(PcPartCriteria.QuantityThreshold + 1, 500),
        Price = faker.Random.Float(1, 2000),
    };

    private static Item CreateNonMatchingItem(Faker faker, int index)
    {
        var item = new Item
        {
            Name = faker.Commerce.ProductName(),
            Quantity = faker.Random.Int(0, 500),
            Price = faker.Random.Float(1, 2000),
        };

        // Rotate three ways of missing the filter, including the quantity boundary.
        switch (index % 3)
        {
            case 0:
                item.Name = $"{PcPartCriteria.NamePrefix} {item.Name}";
                item.Quantity = faker.Random.Int(0, PcPartCriteria.QuantityThreshold);
                break;
            case 1:
                item.Quantity = faker.Random.Int(0, PcPartCriteria.QuantityThreshold);
                break;
        }

        return item;
    }
}
