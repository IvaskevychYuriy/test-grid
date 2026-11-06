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
}
