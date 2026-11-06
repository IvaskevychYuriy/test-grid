namespace TestGrid.Data;

public class Item
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public float Price { get; set; }

    public DateTimeOffset ModifiedOn { get; set; }
}
