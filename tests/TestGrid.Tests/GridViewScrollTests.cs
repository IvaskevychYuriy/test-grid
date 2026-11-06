using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TestGrid.ViewModels;
using TestGrid.Views;

namespace TestGrid.Tests;

public class GridViewScrollTests
{
    [AvaloniaFact]
    public async Task ScrollingToBothEndsLoadsAllRowsWithoutDuplicates()
    {
        using var factory = new TestDbContextFactory();
        await TestData.AddItemsAsync(factory, 200, identicalModifiedOn: false);
        var expected = await TestData.ReadOrderedIdsAsync(factory);
        var viewModel = new GridViewModel(factory) { PageSize = 20 };
        var view = new GridView { DataContext = viewModel };
        var window = new Window { Width = 600, Height = 400, Content = view };
        window.Show();
        var grid = view.GetVisualDescendants().OfType<DataGrid>().Single();
        await WaitUntilStableAsync(viewModel);

        for (var step = 0; step < 40 && viewModel.HasMoreAfter; step++)
        {
            grid.ScrollIntoView(viewModel.Items[^1], null);
            await WaitUntilStableAsync(viewModel);
            AssertContiguousWindow(expected, viewModel);
        }

        Assert.Equal(expected[^1], viewModel.Items[^1].Id);

        for (var step = 0; step < 40 && viewModel.HasMoreBefore; step++)
        {
            grid.ScrollIntoView(viewModel.Items[0], null);
            await WaitUntilStableAsync(viewModel);
            AssertContiguousWindow(expected, viewModel);
        }

        Assert.False(viewModel.HasMoreBefore);
        Assert.Equal(expected[0], viewModel.Items[0].Id);
    }

    private static void AssertContiguousWindow(IReadOnlyList<Guid> expected, GridViewModel viewModel)
    {
        var ids = viewModel.Items.Select(item => item.Id).ToList();
        var start = expected.ToList().IndexOf(ids[0]);
        Assert.True(start >= 0);
        Assert.Equal(expected.Skip(start).Take(ids.Count), ids);
        Assert.True(ids.Count <= viewModel.MaxLoadedRows + viewModel.PageSize);
    }

    private static async Task WaitUntilStableAsync(GridViewModel viewModel)
    {
        var stableTicks = 0;
        var lastSignature = string.Empty;
        for (var tick = 0; tick < 200 && stableTicks < 5; tick++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20, TestContext.Current.CancellationToken);
            var signature = $"{viewModel.Items.Count}:{viewModel.Items.FirstOrDefault()?.Id}";
            stableTicks = signature == lastSignature ? stableTicks + 1 : 0;
            lastSignature = signature;
        }
    }
}
