namespace TestGrid.Data;

public class ModifiedOnDescendingIdAscendingSort : IItemSort
{
    public IOrderedQueryable<Item> Order(IQueryable<Item> items) =>
        items.OrderByDescending(item => item.ModifiedOn).ThenBy(item => item.Id);

    public IOrderedQueryable<Item> OrderReversed(IQueryable<Item> items) =>
        items.OrderBy(item => item.ModifiedOn).ThenByDescending(item => item.Id);

    public IQueryable<Item> After(IQueryable<Item> items, ItemCursor cursor) =>
        items.Where(item => item.ModifiedOn < cursor.ModifiedOn
            || (item.ModifiedOn == cursor.ModifiedOn && item.Id.CompareTo(cursor.Id) > 0));

    public IQueryable<Item> Before(IQueryable<Item> items, ItemCursor cursor) =>
        items.Where(item => item.ModifiedOn > cursor.ModifiedOn
            || (item.ModifiedOn == cursor.ModifiedOn && item.Id.CompareTo(cursor.Id) < 0));

    public IQueryable<Item> NotBefore(IQueryable<Item> items, ItemCursor cursor) =>
        items.Where(item => item.ModifiedOn < cursor.ModifiedOn
            || (item.ModifiedOn == cursor.ModifiedOn && item.Id.CompareTo(cursor.Id) >= 0));

    public IQueryable<Item> NotAfter(IQueryable<Item> items, ItemCursor cursor) =>
        items.Where(item => item.ModifiedOn > cursor.ModifiedOn
            || (item.ModifiedOn == cursor.ModifiedOn && item.Id.CompareTo(cursor.Id) <= 0));

    public ItemCursor CursorOf(Item item) => new(item.ModifiedOn, item.Id);

    // EF stores Guid as upper-case text, so SQL orders ids by that text, not by Guid.CompareTo.
    public int Compare(ItemCursor first, ItemCursor second)
    {
        var byModifiedOn = second.ModifiedOn.CompareTo(first.ModifiedOn);
        return byModifiedOn != 0
            ? byModifiedOn
            : string.CompareOrdinal(IdKey(first.Id), IdKey(second.Id));
    }

    private static string IdKey(Guid id) => id.ToString("D").ToUpperInvariant();
}
