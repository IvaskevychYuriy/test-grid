using CommunityToolkit.Mvvm.ComponentModel;
using TestGrid.Data;

namespace TestGrid.ViewModels;

public partial class ItemViewModel(Item item, ItemCursor cursor) : ObservableObject
{
    private int highlightVersion;

    public Guid Id { get; } = item.Id;

    public ItemCursor Cursor { get; private set; } = cursor;

    [ObservableProperty]
    private string name = item.Name;

    [ObservableProperty]
    private int quantity = item.Quantity;

    [ObservableProperty]
    private float price = item.Price;

    [ObservableProperty]
    private DateTimeOffset modifiedOn = item.ModifiedOn;

    [ObservableProperty]
    private bool isHighlighted;

    public bool ApplyFrom(Item item, ItemCursor cursor)
    {
        var changed = Name != item.Name
            || Quantity != item.Quantity
            || Price != item.Price
            || ModifiedOn != item.ModifiedOn;

        Name = item.Name;
        Quantity = item.Quantity;
        Price = item.Price;
        ModifiedOn = item.ModifiedOn;
        Cursor = cursor;
        return changed;
    }

    public int Highlight()
    {
        IsHighlighted = true;
        return ++highlightVersion;
    }

    public void ClearHighlight(int version)
    {
        if (version == highlightVersion)
        {
            IsHighlighted = false;
        }
    }
}
