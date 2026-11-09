using TestGrid.Data;

namespace TestGrid.Tests;

// ItemCursor only has a DateTimeOffset key slot, so this test sort stores Quantity there as seconds
// since the Unix epoch. It exists to prove the grid takes sort keys from IItemSort, not from ModifiedOn.
public class QuantityDescendingIdAscendingSort : IItemSort
{
    public IOrderedQueryable<Item> Order(IQueryable<Item> items) =>
        items.OrderByDescending(item => item.Quantity).ThenBy(item => item.Id);

    public IOrderedQueryable<Item> OrderReversed(IQueryable<Item> items) =>
        items.OrderBy(item => item.Quantity).ThenByDescending(item => item.Id);

    public IQueryable<Item> After(IQueryable<Item> items, ItemCursor cursor)
    {
        var quantity = QuantityOf(cursor);
        return items.Where(item => item.Quantity < quantity
            || (item.Quantity == quantity && item.Id.CompareTo(cursor.Id) > 0));
    }

    public IQueryable<Item> Before(IQueryable<Item> items, ItemCursor cursor)
    {
        var quantity = QuantityOf(cursor);
        return items.Where(item => item.Quantity > quantity
            || (item.Quantity == quantity && item.Id.CompareTo(cursor.Id) < 0));
    }

    public IQueryable<Item> NotBefore(IQueryable<Item> items, ItemCursor cursor)
    {
        var quantity = QuantityOf(cursor);
        return items.Where(item => item.Quantity < quantity
            || (item.Quantity == quantity && item.Id.CompareTo(cursor.Id) >= 0));
    }

    public IQueryable<Item> NotAfter(IQueryable<Item> items, ItemCursor cursor)
    {
        var quantity = QuantityOf(cursor);
        return items.Where(item => item.Quantity > quantity
            || (item.Quantity == quantity && item.Id.CompareTo(cursor.Id) <= 0));
    }

    public ItemCursor CursorOf(Item item) =>
        new(DateTimeOffset.UnixEpoch.AddSeconds(item.Quantity), item.Id);

    public int Compare(ItemCursor first, ItemCursor second)
    {
        var byQuantity = QuantityOf(second).CompareTo(QuantityOf(first));
        return byQuantity != 0
            ? byQuantity
            : string.CompareOrdinal(IdKey(first.Id), IdKey(second.Id));
    }

    private static int QuantityOf(ItemCursor cursor) =>
        (int)(cursor.ModifiedOn - DateTimeOffset.UnixEpoch).TotalSeconds;

    private static string IdKey(Guid id) => id.ToString("D").ToUpperInvariant();
}
