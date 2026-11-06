# Feature 2: Infinite scroll

## Goal

`GridView` shows 20 rows at a time (hardcoded in `ItemsViewModel`, passed to `GridView`). It loads rows as the user scrolls down, discards rows that are no longer shown, and loads them again on scroll up. It stops when there is no more data.

## Cursor, not paging

Loading uses keyset (cursor) pagination on the sort order `(ModifiedOn desc, Id asc)`:

- Forward: rows after the cursor row `c`, i.e. `ModifiedOn < c.ModifiedOn OR (ModifiedOn = c.ModifiedOn AND Id > c.Id)`, ordered by the sort, `LIMIT n`.
- Backward: the mirrored condition with the reversed order, `LIMIT n`, then reversed in memory.
- Both combined with the filter from feature 1.

## Window

- Page size: 20 (from `ItemsViewModel`).
- Preloading: one extra page of 20 in the scroll direction, so at most 2 x 20 rows are loaded around the viewport. No further optimization.
- Rows outside the window are removed from the `ObservableCollection`.
- `HasMoreBefore` / `HasMoreAfter` flags stop loading at the ends of the data.

## Scrolling

Scroll position is detected from the `ScrollViewer` inside the `DataGrid`. Loading is triggered when the viewport gets near an edge of the loaded window. When rows are prepended or removed from the top, the scroll offset is adjusted so the visible rows do not jump.

## Testing

- First page, next page and previous page return correct rows.
- Ties on `ModifiedOn` do not duplicate or skip rows.
- End of data on both sides.
- Window size never exceeds the limit.

## Smoke test

Scroll down through the whole dataset and back up. No jumps, no duplicates, loading stops at both ends.

## Implementation notes

- `IItemSort` replaces the delegate sort from feature 1. It provides `Order`, `OrderReversed`, `After(cursor)` and `Before(cursor)`; `ModifiedOnDescendingIdAscendingSort` is the only implementation. `GridViewModel.Filter` is unchanged.
- `ItemCursor` is `(ModifiedOn, Id)` taken from the first or last loaded row. `Guid.CompareTo` translates to SQL on SQLite, and TEXT ordering of `Id` matches `ORDER BY`.
- Each load fetches `PageSize + 1` rows. The extra row only tells whether more data exists (`HasMoreBefore` / `HasMoreAfter`), so the end is detected exactly, including when the row count is a multiple of the page size.
- The view tracks realized `DataGridRow`s (`LoadingRow` / `UnloadingRow`), derives the first and last visible item index, and calls `GridViewModel.OnViewportChangedAsync`. All load and trim decisions live in the view model and are unit tested.
- Next page loads when fewer than `PageSize / 4` rows remain below the viewport; previous page the same way above it.
- The loaded window targets `2 x PageSize`. Rows are trimmed from the far side, but never closer than `PageSize / 4` rows to the viewport. The cap is therefore soft: a viewport taller than about 15 rows keeps a few more rows loaded. This also prevents load/trim oscillation.
- The viewport itself shows as many rows as fit the control; `PageSize` is the load chunk, not the visible row count.
- Scroll position needs no compensation: a headless spike showed `DataGrid` keeps the same rows in view when rows are inserted above, or removed above or below.
- Bug found while testing: column bindings were two-way by default, and the grid wrote the formatted `ModifiedOn` text (seconds precision, parsed as local time) back into the view model, corrupting the cursor. All column bindings are now `Mode=OneWay`.
- Tests: view model tests for paging, ties, window bounds and end detection; a headless `GridView` test that scrolls to both ends. The test project moved to xunit v3 (required by `Avalonia.Headless.XUnit`) with the Microsoft.Testing.Platform runner, enabled in `global.json`.
