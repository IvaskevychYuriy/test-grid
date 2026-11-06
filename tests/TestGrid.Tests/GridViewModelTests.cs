using TestGrid.Data;
using TestGrid.ViewModels;

namespace TestGrid.Tests;

public class GridViewModelTests
{
    [Fact]
    public async Task LoadsAllRowsFromDatabase()
    {
        using var factory = new TestDbContextFactory();
        await new DbSeeder(factory).SeedIfEmptyAsync();
        var grid = new GridViewModel(factory);

        await grid.LoadAsync();

        Assert.Equal(DbSeeder.SeedRowCount, grid.Items.Count);
    }

    [Fact]
    public async Task ReloadReplacesRows()
    {
        using var factory = new TestDbContextFactory();
        await new DbSeeder(factory).SeedIfEmptyAsync();
        var grid = new GridViewModel(factory);

        await grid.LoadAsync();
        await grid.LoadAsync();

        Assert.Equal(DbSeeder.SeedRowCount, grid.Items.Count);
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
