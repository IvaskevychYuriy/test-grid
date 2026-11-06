using System.Collections.ObjectModel;
using System.Linq.Expressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TestGrid.Data;

namespace TestGrid.ViewModels;

public partial class GridViewModel(IDbContextFactory<AppDbContext> contextFactory) : ObservableObject
{
    private bool isLoading;

    public ObservableCollection<ItemViewModel> Items { get; } = [];

    public Expression<Func<Item, bool>>? Filter { get; set; }

    public IItemSort Sort { get; set; } = new ModifiedOnDescendingIdAscendingSort();

    public const double RowHeight = 28;

    public const double HeaderHeight = 28;

    public int PageSize { get; set; } = 20;

    public double ViewportHeight => HeaderHeight + (PageSize * RowHeight);

    public bool HasMoreBefore { get; private set; }

    public bool HasMoreAfter { get; private set; }

    public int MaxLoadedRows => PageSize * 2;

    private int EdgeDistance => Math.Max(1, PageSize / 4);

    [RelayCommand]
    public async Task LoadAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var rows = await Sort.Order(FilteredItems(context)).AsNoTracking()
            .Take(PageSize + 1).ToListAsync();

        Items.Clear();
        AppendRows(rows.Take(PageSize));
        HasMoreBefore = false;
        HasMoreAfter = rows.Count > PageSize;
    }

    public async Task OnViewportChangedAsync(int firstVisibleIndex, int lastVisibleIndex)
    {
        if (isLoading || Items.Count == 0)
        {
            return;
        }

        isLoading = true;
        try
        {
            if (HasMoreAfter && lastVisibleIndex >= Items.Count - EdgeDistance)
            {
                await LoadNextAsync(firstVisibleIndex);
            }
            else if (HasMoreBefore && firstVisibleIndex < EdgeDistance)
            {
                await LoadPreviousAsync(lastVisibleIndex);
            }
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadNextAsync(int firstVisibleIndex)
    {
        var last = Items[^1];
        var cursor = new ItemCursor(last.ModifiedOn, last.Id);

        await using var context = await contextFactory.CreateDbContextAsync();
        var rows = await Sort.Order(Sort.After(FilteredItems(context), cursor)).AsNoTracking()
            .Take(PageSize + 1).ToListAsync();

        AppendRows(rows.Take(PageSize));
        HasMoreAfter = rows.Count > PageSize;

        var removable = Math.Max(0, firstVisibleIndex - EdgeDistance);
        var removeCount = Math.Min(Items.Count - MaxLoadedRows, removable);
        for (var removed = 0; removed < removeCount; removed++)
        {
            Items.RemoveAt(0);
            HasMoreBefore = true;
        }
    }

    private async Task LoadPreviousAsync(int lastVisibleIndex)
    {
        var first = Items[0];
        var cursor = new ItemCursor(first.ModifiedOn, first.Id);

        await using var context = await contextFactory.CreateDbContextAsync();
        var rows = await Sort.OrderReversed(Sort.Before(FilteredItems(context), cursor)).AsNoTracking()
            .Take(PageSize + 1).ToListAsync();

        var pageRows = rows.Take(PageSize).ToList();
        foreach (var row in pageRows)
        {
            Items.Insert(0, new ItemViewModel(row));
        }

        HasMoreBefore = rows.Count > PageSize;

        var lastVisibleAfterInsert = lastVisibleIndex + pageRows.Count;
        var removable = Math.Max(0, Items.Count - 1 - lastVisibleAfterInsert - EdgeDistance);
        var removeCount = Math.Min(Items.Count - MaxLoadedRows, removable);
        for (var removed = 0; removed < removeCount; removed++)
        {
            Items.RemoveAt(Items.Count - 1);
            HasMoreAfter = true;
        }
    }

    private IQueryable<Item> FilteredItems(AppDbContext context) =>
        Filter is null ? context.Items : context.Items.Where(Filter);

    private void AppendRows(IEnumerable<Item> rows)
    {
        foreach (var row in rows)
        {
            Items.Add(new ItemViewModel(row));
        }
    }
}
