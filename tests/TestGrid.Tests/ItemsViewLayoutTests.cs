using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TestGrid.Changes;
using TestGrid.ViewModels;
using TestGrid.Views;

namespace TestGrid.Tests;

public class ItemsViewLayoutTests
{
    [AvaloniaFact]
    public async Task ShowsTheChangeLogInAFixedPaneAndFreezesTheHeadWhenScrolling()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(100);
        var page = new ItemsViewModel(
            new GridViewModel(fixture.Factory, fixture.Time, new ImmediateUiDispatcher()));
        var view = new ItemsView { DataContext = page };
        var window = new Window { Width = 900, Height = 900, Content = view };
        window.Show();
        await SettleAsync();

        var grid = view.GetVisualDescendants().OfType<DataGrid>().Single();
        Assert.Equal(page.Grid.ViewportHeight, grid.Bounds.Height);

        for (var batch = 0; batch < 30; batch++)
        {
            await page.Grid.ApplyChangesAsync(new ChangeBatch(1, new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { Guid.NewGuid() }));
        }

        await SettleAsync();
        var logBorder = view.GetVisualDescendants().OfType<Border>().Single(border => border.Height == 182);
        var logLines = logBorder.GetVisualDescendants().OfType<TextBlock>().ToList();
        Assert.Equal(182, logBorder.Bounds.Height);
        Assert.True(logLines.Count > 0);
        Assert.Contains("batch: 1 events", logLines[0].Text);

        for (var step = 0; step < 3; step++)
        {
            grid.ScrollIntoView(page.Grid.Items[^1], null);
            await SettleAsync();
        }

        Assert.True(page.Grid.IsHeadFrozen);
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
