using Microsoft.EntityFrameworkCore;
using TestGrid.Data;

namespace TestGrid.Tests;

public static class TestData
{
    public static readonly DateTimeOffset BaseModifiedOn = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static async Task AddItemsAsync(
        TestDbContextFactory factory,
        int count,
        bool identicalModifiedOn = true)
    {
        if (identicalModifiedOn)
        {
            await using var context = factory.CreateDbContext();
            context.Items.AddRange(Enumerable.Range(0, count).Select(index => CreateItem(index, identicalModifiedOn: true)));
            await context.SaveChangesAsync();
            return;
        }

        for (var index = 0; index < count; index++)
        {
            await using var context = factory.CreateDbContext();
            context.Items.Add(CreateItem(index, identicalModifiedOn: false));
            await context.SaveChangesAsync();
        }
    }

    public static async Task<List<Guid>> ReadOrderedIdsAsync(TestDbContextFactory factory)
    {
        await using var context = factory.CreateDbContext();
        return await new ModifiedOnDescendingIdAscendingSort()
            .Order(context.Items)
            .Select(item => item.Id)
            .ToListAsync();
    }

    private static Item CreateItem(int index, bool identicalModifiedOn) => new()
    {
        Name = $"item {index}",
        Quantity = index,
        ModifiedOn = identicalModifiedOn ? BaseModifiedOn : BaseModifiedOn.AddMilliseconds(index),
    };
}
