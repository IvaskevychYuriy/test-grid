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
