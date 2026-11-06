using Microsoft.EntityFrameworkCore;
using TestGrid.Data;

namespace TestGrid.Tests;

public static class TestData
{
    public static async Task AddItemsAsync(
        TestDbContextFactory factory,
        int count,
        bool identicalModifiedOn = true)
    {
        if (identicalModifiedOn)
        {
            await using var context = factory.CreateDbContext();
            context.Items.AddRange(Enumerable.Range(0, count).Select(CreateItem));
            await context.SaveChangesAsync();
            return;
        }

        for (var index = 0; index < count; index++)
        {
            await using var context = factory.CreateDbContext();
            context.Items.Add(CreateItem(index));
            await context.SaveChangesAsync();
            await Task.Delay(2);
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

    private static Item CreateItem(int index) => new() { Name = $"item {index}", Quantity = index };
}
