using Avalonia.Controls;
using Avalonia.Interactivity;
using TestGrid.ViewModels;

namespace TestGrid.Views;

public partial class GridView : UserControl
{
    public GridView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is GridViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
