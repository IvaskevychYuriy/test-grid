using CommunityToolkit.Mvvm.ComponentModel;
using TestGrid.Data;

namespace TestGrid.ViewModels;

public partial class ItemViewModel(Item item) : ObservableObject
{
    public Guid Id { get; } = item.Id;

    [ObservableProperty]
    private string name = item.Name;

    [ObservableProperty]
    private int quantity = item.Quantity;

    [ObservableProperty]
    private float price = item.Price;

    [ObservableProperty]
    private DateTimeOffset modifiedOn = item.ModifiedOn;
}
