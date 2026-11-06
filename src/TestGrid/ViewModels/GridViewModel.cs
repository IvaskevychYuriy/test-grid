using System.Collections.ObjectModel;
using System.Linq.Expressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TestGrid.Data;

namespace TestGrid.ViewModels;

public partial class GridViewModel(IDbContextFactory<AppDbContext> contextFactory) : ObservableObject
{
    public ObservableCollection<ItemViewModel> Items { get; } = [];

    public Expression<Func<Item, bool>>? Filter { get; set; }

    public Func<IQueryable<Item>, IOrderedQueryable<Item>>? Sort { get; set; }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        IQueryable<Item> query = context.Items.AsNoTracking();

        if (Filter is not null)
        {
            query = query.Where(Filter);
        }

        if (Sort is not null)
        {
            query = Sort(query);
        }

        var items = await query.ToListAsync();

        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new ItemViewModel(item));
        }
    }
}
