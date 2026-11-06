namespace TestGrid.Data;

public interface IItemSort
{
    IOrderedQueryable<Item> Order(IQueryable<Item> items);

    IOrderedQueryable<Item> OrderReversed(IQueryable<Item> items);

    IQueryable<Item> After(IQueryable<Item> items, ItemCursor cursor);

    IQueryable<Item> Before(IQueryable<Item> items, ItemCursor cursor);
}
