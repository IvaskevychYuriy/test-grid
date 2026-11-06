using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TestGrid.ViewModels;
using TestGrid.Views;

namespace TestGrid.Tests;

public class GridViewViewportTests
{
    [AvaloniaTheory]
    [InlineData(700)]
    [InlineData(1100)]
    public async Task ShowsExactlyTwentyRowsRegardlessOfWindowHeight(double windowHeight)
    {
        using var factory = new TestDbContextFactory();
        await TestData.AddItemsAsync(factory, 100);
        var viewModel = new GridViewModel(factory) { PageSize = 20 };
        var view = new GridView { DataContext = viewModel };
        var window = new Window { Width = 800, Height = windowHeight, Content = view };
        window.Show();
        await SettleAsync();

        var grid = view.GetVisualDescendants().OfType<DataGrid>().Single();
        var fullyVisibleRows = grid.GetVisualDescendants().OfType<DataGridRow>()
            .Where(row => row.DataContext is ItemViewModel)
            .Count(row =>
            {
                var top = row.TranslatePoint(new Point(0, 0), grid)?.Y ?? -1;
                return top >= 0 && top + row.Bounds.Height <= grid.Bounds.Height + 0.5;
            });

        Assert.Equal(viewModel.ViewportHeight, grid.Bounds.Height);
        Assert.Equal(20, fullyVisibleRows);
    }

    private static async Task SettleAsync()
    {
        for (var tick = 0; tick < 10; tick++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(30, TestContext.Current.CancellationToken);
        }
    }
}
