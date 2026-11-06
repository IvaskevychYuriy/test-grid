using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TestGrid.Data;

public class ModifiedOnInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        StampModifiedItems(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StampModifiedItems(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void StampModifiedItems(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<Item>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.ModifiedOn = now;
            }
        }
    }
}
