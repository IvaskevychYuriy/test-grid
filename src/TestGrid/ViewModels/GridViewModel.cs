using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TestGrid.Data;

namespace TestGrid.ViewModels;

public partial class GridViewModel(IDbContextFactory<AppDbContext> contextFactory) : ObservableObject
{
    public ObservableCollection<ItemViewModel> Items { get; } = [];

    [RelayCommand]
    public async Task LoadAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var items = await context.Items.AsNoTracking().ToListAsync();

        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new ItemViewModel(item));
        }
    }
}
