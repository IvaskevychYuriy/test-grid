using CommunityToolkit.Mvvm.ComponentModel;
using TestGrid.Data;

namespace TestGrid.ViewModels;

public class ItemsViewModel : ObservableObject
{
    public ItemsViewModel(GridViewModel grid)
    {
        Grid = grid;
        Grid.Filter = item => item.Quantity > PcPartCriteria.QuantityThreshold
            && item.Name.StartsWith(PcPartCriteria.NamePrefix);
        Grid.Sort = new ModifiedOnDescendingIdAscendingSort();
        Grid.PageSize = PageSize;
    }

    public int PageSize { get; } = 20;

    public GridViewModel Grid { get; }

    public string ConditionsDescription =>
        "Filter: Quantity > 10 and Name starts with \"pc part\". Sort: Modified on (newest first), then Id.";
}
