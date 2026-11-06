using TestGrid.Data;
using TestGrid.ViewModels;

namespace TestGrid.Tests;

public class GridViewModelTests
{
    [Fact]
    public async Task LoadsFirstPageOnly()
    {
        using var factory = new TestDbContextFactory();
        await new DbSeeder(factory).SeedIfEmptyAsync();
        var grid = new GridViewModel(factory) { PageSize = 20 };

        await grid.LoadAsync();

        Assert.Equal(20, grid.Items.Count);
        Assert.True(grid.HasMoreAfter);
        Assert.False(grid.HasMoreBefore);
    }

    [Fact]
    public async Task ReloadReplacesRows()
    {
        using var factory = new TestDbContextFactory();
        await new DbSeeder(factory).SeedIfEmptyAsync();
        var grid = new GridViewModel(factory) { PageSize = 20 };

        await grid.LoadAsync();
        await grid.LoadAsync();

        Assert.Equal(20, grid.Items.Count);
    }

    [Fact]
    public async Task StopsAtEndWhenFewerRowsThanPageSize()
    {
        using var factory = new TestDbContextFactory();
        await TestData.AddItemsAsync(factory, 5);
        var grid = new GridViewModel(factory) { PageSize = 20 };

        await grid.LoadAsync();

        Assert.Equal(5, grid.Items.Count);
        Assert.False(grid.HasMoreAfter);
        Assert.False(grid.HasMoreBefore);
    }

    [Fact]
    public async Task StopsAtEndWhenRowCountIsExactlyPageSize()
    {
        using var factory = new TestDbContextFactory();
        await TestData.AddItemsAsync(factory, 20);
        var grid = new GridViewModel(factory) { PageSize = 20 };

        await grid.LoadAsync();

        Assert.False(grid.HasMoreAfter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScrollingDownAndBackUpVisitsEveryRowOnceInOrder(bool identicalModifiedOn)
    {
        using var factory = new TestDbContextFactory();
        await TestData.AddItemsAsync(factory, 130, identicalModifiedOn);
        var expected = await TestData.ReadOrderedIdsAsync(factory);
        var grid = new GridViewModel(factory) { PageSize = 20 };
        await grid.LoadAsync();

        var seenDown = new HashSet<Guid>(grid.Items.Select(item => item.Id));
        while (grid.HasMoreAfter)
        {
            await grid.OnViewportChangedAsync(grid.Items.Count - 10, grid.Items.Count - 1);
            AssertContiguousSlice(expected, grid);
            Assert.True(grid.Items.Count <= grid.MaxLoadedRows);
            seenDown.UnionWith(grid.Items.Select(item => item.Id));
        }

        Assert.Equal(expected.Count, seenDown.Count);
        Assert.Equal(expected[^1], grid.Items[^1].Id);

        while (grid.HasMoreBefore)
        {
            await grid.OnViewportChangedAsync(0, 9);
            AssertContiguousSlice(expected, grid);
            Assert.True(grid.Items.Count <= grid.MaxLoadedRows);
        }

        Assert.Equal(expected[0], grid.Items[0].Id);
    }

    [Fact]
    public async Task DoesNotLoadWhenViewportIsFarFromEdges()
    {
        using var factory = new TestDbContextFactory();
        await TestData.AddItemsAsync(factory, 100);
        var grid = new GridViewModel(factory) { PageSize = 20 };
        await grid.LoadAsync();

        await grid.OnViewportChangedAsync(2, 10);

        Assert.Equal(20, grid.Items.Count);
    }

    [Fact]
    public async Task KeepsVisibleRowsWhenTrimming()
    {
        using var factory = new TestDbContextFactory();
        await TestData.AddItemsAsync(factory, 100);
        var grid = new GridViewModel(factory) { PageSize = 20 };
        await grid.LoadAsync();
        await grid.OnViewportChangedAsync(10, 19);
        Assert.Equal(40, grid.Items.Count);
        var visible = grid.Items.Skip(30).Take(10).Select(item => item.Id).ToList();

        await grid.OnViewportChangedAsync(30, 39);

        Assert.Equal(40, grid.Items.Count);
        Assert.Equal(visible, grid.Items.Skip(10).Take(10).Select(item => item.Id));
        Assert.True(grid.HasMoreBefore);
    }

    private static void AssertContiguousSlice(IReadOnlyList<Guid> expected, GridViewModel grid)
    {
        var ids = grid.Items.Select(item => item.Id).ToList();
        var start = expected.ToList().IndexOf(ids[0]);
        Assert.True(start >= 0);
        Assert.Equal(expected.Skip(start).Take(ids.Count), ids);
    }

    [Fact]
    public void ItemViewModelRaisesPropertyChanged()
    {
        var item = new ItemViewModel(new Item { Name = "a" });
        var changed = new List<string?>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        item.Quantity = 5;

        Assert.Contains(nameof(ItemViewModel.Quantity), changed);
    }
}
