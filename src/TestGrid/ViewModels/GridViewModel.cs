using System.Collections.ObjectModel;
using System.Linq.Expressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TestGrid.Changes;
using TestGrid.Data;

namespace TestGrid.ViewModels;

public partial class GridViewModel(
    IDbContextFactory<AppDbContext> contextFactory,
    TimeProvider? timeProvider = null,
    IUiDispatcher? dispatcher = null) : ObservableObject
{
    public const double RowHeight = 28;

    public const double HeaderHeight = 28;

    private const int InClauseChunkSize = 500;

    private const int MaxLogLines = 200;

    private static readonly TimeSpan HighlightDuration = TimeSpan.FromSeconds(1);

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly IUiDispatcher uiDispatcher = dispatcher ?? new ImmediateUiDispatcher();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<Guid, ItemViewModel> loadedById = [];
    private int firstVisibleIndex;
    private int lastVisibleIndex;

    public ObservableCollection<ItemViewModel> Items { get; } = [];

    public ObservableCollection<string> ChangeLog { get; } = [];

    public Expression<Func<Item, bool>>? Filter { get; set; }

    public IItemSort Sort { get; set; } = new ModifiedOnDescendingIdAscendingSort();

    public int PageSize { get; set; } = 20;

    public double ViewportHeight => HeaderHeight + (PageSize * RowHeight);

    public bool HasMoreBefore { get; private set; }

    public bool HasMoreAfter { get; private set; }

    public bool IsHeadFrozen { get; private set; }

    public int MaxLoadedRows => PageSize * 2;

    private int EdgeDistance => Math.Max(1, PageSize / 4);

    private bool IsLowerBounded => HasMoreBefore || IsHeadFrozen;

    [RelayCommand]
    public async Task LoadAsync()
    {
        await gate.WaitAsync();
        try
        {
            await LoadHeadAsync();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task OnViewportChangedAsync(int firstVisible, int lastVisible)
    {
        firstVisibleIndex = firstVisible;
        lastVisibleIndex = lastVisible;
        if (Items.Count == 0 || !gate.Wait(0))
        {
            return;
        }

        try
        {
            if (!HasMoreBefore && !IsHeadFrozen && firstVisible > 0)
            {
                IsHeadFrozen = true;
                PrependLog(["browsing: head frozen, new rows above the window are ignored"]);
            }

            if (IsHeadFrozen && !HasMoreBefore && firstVisible == 0)
            {
                await RefreshHeadAsync();
            }
            else if (HasMoreAfter && lastVisible >= Items.Count - EdgeDistance)
            {
                await LoadNextAsync(firstVisible);
            }
            else if (HasMoreBefore && firstVisible < EdgeDistance)
            {
                await LoadPreviousAsync(lastVisible);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ApplyChangesAsync(ChangeBatch batch)
    {
        await gate.WaitAsync();
        try
        {
            await ReconcileAsync(batch);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task LoadHeadAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var rows = await Sort.Order(FilteredItems(context)).AsNoTracking()
            .Take(PageSize + 1).ToListAsync();

        ClearRows();
        foreach (var row in rows.Take(PageSize))
        {
            AppendRow(row);
        }

        HasMoreBefore = false;
        HasMoreAfter = rows.Count > PageSize;
        IsHeadFrozen = false;
        firstVisibleIndex = 0;
        lastVisibleIndex = Math.Max(0, Items.Count - 1);
    }

    private async Task LoadNextAsync(int firstVisible)
    {
        var cursor = Items[^1].Cursor;

        await using var context = await contextFactory.CreateDbContextAsync();
        var rows = await Sort.Order(Sort.After(FilteredItems(context), cursor)).AsNoTracking()
            .Take(PageSize + 1).ToListAsync();

        foreach (var row in rows.Take(PageSize))
        {
            AppendRow(row);
        }

        HasMoreAfter = rows.Count > PageSize;

        var removable = Math.Max(0, firstVisible - EdgeDistance);
        var removeCount = Math.Min(Items.Count - MaxLoadedRows, removable);
        for (var removed = 0; removed < removeCount; removed++)
        {
            RemoveRowAt(0);
            HasMoreBefore = true;
        }
    }

    private async Task LoadPreviousAsync(int lastVisible)
    {
        var cursor = Items[0].Cursor;

        await using var context = await contextFactory.CreateDbContextAsync();
        var rows = await Sort.OrderReversed(Sort.Before(FilteredItems(context), cursor)).AsNoTracking()
            .Take(PageSize + 1).ToListAsync();

        var pageRows = rows.Take(PageSize).ToList();
        foreach (var row in pageRows)
        {
            InsertRow(0, CreateRow(row));
        }

        HasMoreBefore = rows.Count > PageSize;
        if (!HasMoreBefore)
        {
            IsHeadFrozen = false;
        }

        var lastVisibleAfterInsert = lastVisible + pageRows.Count;
        var removable = Math.Max(0, Items.Count - 1 - lastVisibleAfterInsert - EdgeDistance);
        var removeCount = Math.Min(Items.Count - MaxLoadedRows, removable);
        for (var removed = 0; removed < removeCount; removed++)
        {
            RemoveRowAt(Items.Count - 1);
            HasMoreAfter = true;
        }
    }

    private async Task RefreshHeadAsync()
    {
        var count = Math.Clamp(Items.Count, PageSize, MaxLoadedRows);

        await using var context = await contextFactory.CreateDbContextAsync();
        var rows = await Sort.Order(FilteredItems(context)).AsNoTracking()
            .Take(count + 1).ToListAsync();

        var changed = SyncRows(rows.Take(count).ToList());
        HasMoreBefore = false;
        HasMoreAfter = rows.Count > count;
        IsHeadFrozen = false;

        Highlight(changed);
        PrependLog([$"live: head refreshed, {changed.Count} rows changed"]);
    }

    private List<ItemViewModel> SyncRows(IReadOnlyList<Item> fresh)
    {
        var freshIds = fresh.Select(item => item.Id).ToHashSet();
        for (var index = Items.Count - 1; index >= 0; index--)
        {
            if (!freshIds.Contains(Items[index].Id))
            {
                RemoveRowAt(index);
            }
        }

        var changed = new List<ItemViewModel>();
        for (var index = 0; index < fresh.Count; index++)
        {
            var item = fresh[index];
            if (index < Items.Count && Items[index].Id == item.Id)
            {
                if (Items[index].ApplyFrom(item, Sort.CursorOf(item)))
                {
                    changed.Add(Items[index]);
                }

                continue;
            }

            if (loadedById.TryGetValue(item.Id, out var displaced))
            {
                RemoveRowAt(Items.IndexOf(displaced));
            }

            changed.Add(InsertRow(index, CreateRow(item)));
        }

        return changed;
    }

    private async Task ReconcileAsync(ChangeBatch batch)
    {
        var log = new List<string>
        {
            $"batch: {batch.EventCount} events -> {batch.Inserted.Count} inserts, "
                + $"{batch.Updated.Count} updates, {batch.Deleted.Count} deletes",
        };

        if (Items.Count == 0)
        {
            await LoadHeadAsync();
            log.Add("window was empty, reloaded from the head");
            PrependLog(log);
            return;
        }

        var targetCount = Math.Min(Items.Count, MaxLoadedRows);
        ItemCursor? lowerBound = IsLowerBounded ? Items[0].Cursor : null;
        ItemCursor? upperBound = HasMoreAfter ? Items[^1].Cursor : null;
        var highlighted = new List<ItemViewModel>();
        var ignored = 0;

        foreach (var id in batch.Deleted)
        {
            if (loadedById.TryGetValue(id, out var row))
            {
                RemoveRowAt(Items.IndexOf(row));
                log.Add($"delete \"{row.Name}\": removed from the window");
            }
            else
            {
                ignored++;
            }
        }

        var upserted = batch.Upserted.ToList();
        var inRange = await QueryInRangeAsync(upserted, lowerBound, upperBound);
        foreach (var id in upserted)
        {
            var isLoaded = loadedById.TryGetValue(id, out var row);
            var isInRange = inRange.TryGetValue(id, out var item);

            if (isLoaded && isInRange)
            {
                ApplyUpdate(row!, item!, log, highlighted);
            }
            else if (isLoaded)
            {
                RemoveRowAt(Items.IndexOf(row!));
                log.Add($"update \"{row!.Name}\": left the loaded range or the filter, removed");
            }
            else if (isInRange)
            {
                var inserted = CreateRow(item!);
                var index = FindInsertIndex(inserted.Cursor);
                highlighted.Add(InsertRow(index, inserted));
                log.Add($"insert \"{inserted.Name}\": added at row {index}");
            }
            else
            {
                ignored++;
            }
        }

        var trimmed = TrimToCapacity();
        if (trimmed > 0)
        {
            log.Add($"trimmed {trimmed} rows far from the viewport");
        }

        await RefillAsync(targetCount - Items.Count, log, highlighted);

        Highlight(highlighted);
        if (ignored > 0)
        {
            log.Add($"ignored {ignored} changes outside the loaded range");
        }

        PrependLog(log);
    }

    private void ApplyUpdate(ItemViewModel row, Item item, List<string> log, List<ItemViewModel> highlighted)
    {
        var cursor = Sort.CursorOf(item);
        var sortKeyChanged = Sort.Compare(row.Cursor, cursor) != 0;
        var changed = row.ApplyFrom(item, cursor);

        if (sortKeyChanged)
        {
            RemoveRowAt(Items.IndexOf(row));
            var index = FindInsertIndex(row.Cursor);
            InsertRow(index, row);
            log.Add($"update \"{row.Name}\": moved to row {index}");
        }
        else if (changed)
        {
            log.Add($"update \"{row.Name}\": patched in place");
        }

        if (changed)
        {
            highlighted.Add(row);
        }
    }

    private async Task RefillAsync(int missing, List<string> log, List<ItemViewModel> highlighted)
    {
        if (missing <= 0)
        {
            return;
        }

        if (Items.Count == 0)
        {
            await LoadHeadAsync();
            highlighted.AddRange(Items);
            log.Add("window became empty, reloaded from the head");
            return;
        }

        var refilled = 0;
        await using var context = await contextFactory.CreateDbContextAsync();

        if (HasMoreAfter)
        {
            var rows = await Sort.Order(Sort.After(FilteredItems(context), Items[^1].Cursor)).AsNoTracking()
                .Take(missing + 1).ToListAsync();
            foreach (var row in rows.Take(missing))
            {
                highlighted.Add(AppendRow(row));
                refilled++;
            }

            HasMoreAfter = rows.Count > missing;
        }

        if (refilled < missing && !HasMoreAfter && HasMoreBefore)
        {
            var wanted = missing - refilled;
            var rows = await Sort.OrderReversed(Sort.Before(FilteredItems(context), Items[0].Cursor)).AsNoTracking()
                .Take(wanted + 1).ToListAsync();
            foreach (var row in rows.Take(wanted))
            {
                highlighted.Add(InsertRow(0, CreateRow(row)));
                refilled++;
            }

            HasMoreBefore = rows.Count > wanted;
            if (!HasMoreBefore)
            {
                IsHeadFrozen = false;
            }
        }

        if (refilled > 0)
        {
            log.Add($"refilled {refilled} rows");
        }
    }

    private async Task<Dictionary<Guid, Item>> QueryInRangeAsync(
        IReadOnlyList<Guid> ids,
        ItemCursor? lowerBound,
        ItemCursor? upperBound)
    {
        var result = new Dictionary<Guid, Item>();
        if (ids.Count == 0)
        {
            return result;
        }

        await using var context = await contextFactory.CreateDbContextAsync();
        foreach (var chunk in ids.Chunk(InClauseChunkSize))
        {
            var chunkIds = chunk.ToList();
            var query = FilteredItems(context).Where(item => chunkIds.Contains(item.Id));
            if (lowerBound is { } lower)
            {
                query = Sort.NotBefore(query, lower);
            }

            if (upperBound is { } upper)
            {
                query = Sort.NotAfter(query, upper);
            }

            foreach (var item in await query.AsNoTracking().ToListAsync())
            {
                result[item.Id] = item;
            }
        }

        return result;
    }

    private int TrimToCapacity()
    {
        var removed = 0;
        while (Items.Count > MaxLoadedRows)
        {
            var topSlack = firstVisibleIndex - EdgeDistance;
            var bottomSlack = Items.Count - 1 - lastVisibleIndex - EdgeDistance;
            if (bottomSlack > 0 && bottomSlack >= topSlack)
            {
                RemoveRowAt(Items.Count - 1);
                HasMoreAfter = true;
            }
            else if (topSlack > 0)
            {
                RemoveRowAt(0);
                HasMoreBefore = true;
            }
            else
            {
                break;
            }

            removed++;
        }

        return removed;
    }

    private int FindInsertIndex(ItemCursor cursor)
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (Sort.Compare(cursor, Items[index].Cursor) < 0)
            {
                return index;
            }
        }

        return Items.Count;
    }

    private IQueryable<Item> FilteredItems(AppDbContext context) =>
        Filter is null ? context.Items : context.Items.Where(Filter);

    private ItemViewModel CreateRow(Item item) => new(item, Sort.CursorOf(item));

    private ItemViewModel AppendRow(Item item)
    {
        var row = CreateRow(item);
        loadedById[row.Id] = row;
        Items.Add(row);
        return row;
    }

    private ItemViewModel InsertRow(int index, ItemViewModel row)
    {
        loadedById[row.Id] = row;
        Items.Insert(index, row);
        if (index <= firstVisibleIndex)
        {
            firstVisibleIndex++;
            lastVisibleIndex++;
        }
        else if (index <= lastVisibleIndex)
        {
            lastVisibleIndex++;
        }

        return row;
    }

    private void RemoveRowAt(int index)
    {
        loadedById.Remove(Items[index].Id);
        Items.RemoveAt(index);
        if (index < firstVisibleIndex)
        {
            firstVisibleIndex--;
            lastVisibleIndex--;
        }
        else if (index <= lastVisibleIndex)
        {
            lastVisibleIndex--;
        }
    }

    private void ClearRows()
    {
        loadedById.Clear();
        Items.Clear();
    }

    private void Highlight(IReadOnlyCollection<ItemViewModel> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var highlights = rows.Distinct().Select(row => (Row: row, Version: row.Highlight())).ToList();
        ITimer? timer = null;
        timer = clock.CreateTimer(
            _ =>
            {
                uiDispatcher.Post(() =>
                {
                    foreach (var (row, version) in highlights)
                    {
                        row.ClearHighlight(version);
                    }
                });
                timer?.Dispose();
            },
            state: null,
            dueTime: HighlightDuration,
            period: Timeout.InfiniteTimeSpan);
    }

    private void PrependLog(IReadOnlyList<string> lines)
    {
        var timestamp = clock.GetLocalNow().ToString("HH:mm:ss");
        for (var index = lines.Count - 1; index >= 0; index--)
        {
            ChangeLog.Insert(0, $"{timestamp} {lines[index]}");
        }

        while (ChangeLog.Count > MaxLogLines)
        {
            ChangeLog.RemoveAt(ChangeLog.Count - 1);
        }
    }
}
