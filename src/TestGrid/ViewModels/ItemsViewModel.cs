using CommunityToolkit.Mvvm.ComponentModel;

namespace TestGrid.ViewModels;

public class ItemsViewModel(GridViewModel grid) : ObservableObject
{
    public GridViewModel Grid { get; } = grid;
}
