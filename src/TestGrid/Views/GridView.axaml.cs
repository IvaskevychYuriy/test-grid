using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TestGrid.ViewModels;

namespace TestGrid.Views;

public partial class GridView : UserControl
{
    private readonly HashSet<DataGridRow> realizedRows = [];
    private bool viewportCheckQueued;

    public GridView()
    {
        InitializeComponent();
        ItemsGrid.LoadingRow += (_, e) => OnRowRealizationChanged(e.Row, isRealized: true);
        ItemsGrid.UnloadingRow += (_, e) => OnRowRealizationChanged(e.Row, isRealized: false);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is GridViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }

    private void OnRowRealizationChanged(DataGridRow row, bool isRealized)
    {
        if (isRealized)
        {
            realizedRows.Add(row);
        }
        else
        {
            realizedRows.Remove(row);
        }

        if (!viewportCheckQueued)
        {
            viewportCheckQueued = true;
            Dispatcher.UIThread.Post(CheckViewportAsync, DispatcherPriority.Background);
        }
    }

    private async void CheckViewportAsync()
    {
        viewportCheckQueued = false;
        if (DataContext is not GridViewModel viewModel)
        {
            return;
        }

        var visibleIndexes = realizedRows
            .Select(row => row.DataContext)
            .OfType<ItemViewModel>()
            .Select(viewModel.Items.IndexOf)
            .Where(index => index >= 0)
            .ToList();
        if (visibleIndexes.Count == 0)
        {
            return;
        }

        await viewModel.OnViewportChangedAsync(visibleIndexes.Min(), visibleIndexes.Max());
    }
}
