using Microsoft.EntityFrameworkCore;
using TestGrid.Data;

namespace TestGrid.Tests;

public class ModifiedOnInterceptorTests
{
    [Fact]
    public async Task SetsModifiedOnInUtcWhenItemIsCreated()
    {
        using var factory = new TestDbContextFactory();
        var before = DateTimeOffset.UtcNow;

        await using (var context = factory.CreateDbContext())
        {
            context.Items.Add(new Item { Name = "new" });
            await context.SaveChangesAsync();
        }

        await using var readContext = factory.CreateDbContext();
        var item = await readContext.Items.SingleAsync();
        Assert.True(item.ModifiedOn >= before);
        Assert.Equal(TimeSpan.Zero, item.ModifiedOn.Offset);
    }

    [Fact]
    public async Task UpdatesModifiedOnWhenItemChanges()
    {
        using var factory = new TestDbContextFactory();
        var id = Guid.NewGuid();
        await using (var context = factory.CreateDbContext())
        {
            context.Items.Add(new Item { Id = id, Name = "old" });
            await context.SaveChangesAsync();
        }

        DateTimeOffset createdOn;
        await using (var context = factory.CreateDbContext())
        {
            createdOn = (await context.Items.SingleAsync()).ModifiedOn;
        }

        await Task.Delay(20);
        await using (var context = factory.CreateDbContext())
        {
            var item = await context.Items.SingleAsync();
            item.Name = "changed";
            context.SaveChanges();
        }

        await using var readContext = factory.CreateDbContext();
        var updated = await readContext.Items.SingleAsync();
        Assert.True(updated.ModifiedOn > createdOn);
    }
}
