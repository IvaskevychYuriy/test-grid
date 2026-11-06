using Microsoft.EntityFrameworkCore;
using TestGrid.Data;

namespace TestGrid.Tests;

public class SortOrderTests
{
    [Fact]
    public async Task InMemoryComparerMatchesSqlOrderIncludingTies()
    {
        using var factory = new TestDbContextFactory();
        var random = new Random(7);
        await using (var context = factory.CreateDbContext())
        {
            context.Items.AddRange(Enumerable.Range(0, 400).Select(index => new Item
            {
                Id = Guid.NewGuid(),
                Name = $"item {index}",
                Quantity = index,
                ModifiedOn = TestData.BaseModifiedOn.AddSeconds(random.Next(5)),
            }));
            await context.SaveChangesAsync();
        }

        var sort = new ModifiedOnDescendingIdAscendingSort();
        await using var readContext = factory.CreateDbContext();
        var sqlOrder = await sort.Order(readContext.Items.AsNoTracking()).ToListAsync();

        var memoryOrder = sqlOrder
            .OrderBy(_ => random.Next())
            .Select(item => new ItemCursor(item.ModifiedOn, item.Id))
            .ToList();
        memoryOrder.Sort(sort.Compare);

        Assert.Equal(sqlOrder.Select(item => item.Id), memoryOrder.Select(cursor => cursor.Id));
    }

    [Fact]
    public async Task HeadAndCursorQueriesUseTheSortIndex()
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateDbContext();

        var headPlan = await ExplainAsync(
            context,
            "SELECT \"Id\" FROM \"Items\" WHERE \"Quantity\" > 10 AND \"Name\" LIKE 'pc part%' "
            + "ORDER BY \"ModifiedOn\" DESC, \"Id\" LIMIT 21");
        var cursorPlan = await ExplainAsync(
            context,
            "SELECT \"Id\" FROM \"Items\" WHERE \"Quantity\" > 10 AND "
            + "(\"ModifiedOn\" < 5 OR (\"ModifiedOn\" = 5 AND \"Id\" > 'A')) "
            + "ORDER BY \"ModifiedOn\" DESC, \"Id\" LIMIT 21");

        Assert.Contains("IX_Items_ModifiedOn_Id", headPlan);
        Assert.DoesNotContain("TEMP B-TREE", headPlan);
        Assert.Contains("IX_Items_ModifiedOn_Id", cursorPlan);
        Assert.DoesNotContain("TEMP B-TREE", cursorPlan);
    }

    [Fact]
    public async Task ChangedIdLookupUsesThePrimaryKey()
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateDbContext();

        var plan = await ExplainAsync(
            context,
            "SELECT \"Id\" FROM \"Items\" WHERE \"Id\" IN ('A', 'B') AND \"Quantity\" > 10");

        Assert.Contains("SEARCH", plan);
        Assert.DoesNotContain("SCAN", plan);
    }

    private static async Task<string> ExplainAsync(AppDbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + sql;
        await using var reader = await command.ExecuteReaderAsync();
        var details = new List<string>();
        while (await reader.ReadAsync())
        {
            details.Add(reader.GetString(3));
        }

        return string.Join(" | ", details);
    }
}
