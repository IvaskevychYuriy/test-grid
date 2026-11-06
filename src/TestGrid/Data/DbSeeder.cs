using Bogus;
using Microsoft.EntityFrameworkCore;

namespace TestGrid.Data;

public class DbSeeder(IDbContextFactory<AppDbContext> contextFactory)
{
    public const int SeedRowCount = 1000;

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

    public static List<Item> GenerateItems(int count)
    {
        var faker = new Faker<Item>()
            .RuleFor(i => i.Id, _ => Guid.NewGuid())
            .RuleFor(i => i.Name, f => f.Commerce.ProductName())
            .RuleFor(i => i.Quantity, f => f.Random.Int(0, 500))
            .RuleFor(i => i.Price, f => f.Random.Float(1, 2000));

        return faker.Generate(count);
    }
}
