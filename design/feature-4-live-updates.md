# Feature 4: Live updates

## Goal

The grid reacts to `IDataChangeNotifier.DataChanged`. Events are batched for 2 seconds, merged, and applied to the loaded rows with as few queries as possible. Changed rows blink for 1 second and a log pane under the grid says what happened.

Background reading (user's design doc "Live feed + virtual scroll with after-image-only CDC events"): the loaded window is a contiguous range `[lo, hi]` of the sort order plus two flags that say whether either end is the end of the result set. Events are after-images. This feature implements that model on the client side.

## Batching

- `ChangeBatcher` subscribes to the notifier. The first event opens a window of `DataChangeOptions.BatchWindow` (2 s). The window does not reset on later events (the worker emits about 12 events per second, a resetting debounce would never fire). When it expires, one `ChangeBatch` is raised. Without events nothing runs.
- Aggregation per item id gives the net effect: Insert+Update is an insert, Insert+Delete cancels out, Update+Delete is a delete, Update+Update is one update. `ChangeBatch` holds `Inserted`, `Updated`, `Deleted` and the raw event count.
- `TimeProvider` is injected so tests control time.

## State of the loaded window

| Idea in the doc | In this app |
| --- | --- |
| `lo`, `hi` | cursor of the first and last row of `GridViewModel.Items` |
| `atHead` | `!HasMoreBefore` |
| `atTail` | `!HasMoreAfter` |
| live mode | window includes the head and the viewport is on the first row |
| browsing mode | the user scrolled down; head events are not applied |

Leaving the top (first visible index above 0) while `!HasMoreBefore` sets `IsHeadFrozen`: the lower bound is then the first loaded row, so rows that sort above it are ignored and nothing shifts under the viewport. When the viewport returns to row 0, the head is refetched fresh and applied as a diff by id (no scroll reset); live mode resumes.

## Reconcile

Runs on the UI thread under the same single-flight gate as scroll loading, so the two never interleave.

1. Deleted ids: remove them from `Items` when loaded; otherwise ignore.
2. Inserted and updated ids: one query per chunk of 500 ids, `Id IN (...)` plus the filter plus the range bounds (`NotBefore(lo)` when lower bounded, `NotAfter(hi)` when `HasMoreAfter`). It is a primary key lookup with a range test; the filter is never re-implemented in code. The result is the set of rows that are in range now.
3. For every id:

| Loaded | In range now | Action |
| --- | --- | --- |
| yes | yes | patch the row in place; if `ModifiedOn` changed, move it to its new position |
| yes | no | remove from `Items` |
| no | yes | insert at its position |
| no | no | nothing (counted as ignored) |

4. The position of an insert or move is found in memory with `IItemSort.Compare`. It must equal the SQL order, so `Id` is compared as the uppercase text EF stores (`Guid.CompareTo` orders differently). A test compares both orders.
5. Trim: if the window exceeds `MaxLoadedRows`, rows are removed from the side farther from the viewport, never closer than `EdgeDistance` to it.
6. Refill: if rows were removed, the window is refilled to its previous size with one cursor query after `hi` (then one before `lo` when the tail was reached). An empty window is reloaded from the head.

Deviation from the doc: the doc does not backfill after a removal ("the next scroll loads more"). The requirement here is that a delete inside the viewed window fetches rows, so the window keeps its size.

## Performance

- Per batch: one PK `IN` query per 500 changed ids, plus at most two `LIMIT` cursor queries for the refill. No `COUNT`, no re-query of the whole window.
- Migration `AddItemsSortIndex` adds an index on `(ModifiedOn DESC, Id)`, so cursor and head queries seek on the index instead of sorting the table. A test checks the query plan uses it.
- The filter `Name LIKE 'pc part%'` is case-insensitive in SQLite and cannot use a plain index; it filters while scanning the sort index. Revisit with a `COLLATE NOCASE` index if the table grows large.

## Visual feedback and log

- `ItemViewModel.IsHighlighted` is set for inserted, patched, moved and refilled rows and cleared after 1 s (a per-row version guards against an earlier timer clearing a newer highlight). `DataGridRow` gets a `highlighted` class that sets a background with a short transition.
- `GridViewModel.ChangeLog` (newest first, capped at 200 lines) is shown in a fixed 10-row pane under the grid: a batch summary line, one line per change that affected the view, an `ignored N changes outside the loaded range` line, refill lines and mode changes.
- `MainWindow` minimum size fits label, 20-row grid and log pane; the grid and log never grow.

## Wiring

`ItemsViewModel` (the page) forwards batches from `ChangeBatcher` to `GridViewModel.ApplyChangesAsync` through `IUiDispatcher`. `GridViewModel` stays free of the notifier so it is easy to test.

## Testing

- Batcher: no events, no flush; one batch at expiry; window does not reset; next window after a flush.
- Aggregation table.
- Reconcile against a seeded database for windows at the head, in the middle and at the tail: insert above, inside and below the range, matching and not matching; update in place; update moving to the top (live and browsing); update that stops or starts matching; delete in the window with refill; delete outside the window; window cap; no duplicates; the result equals a fresh query of the same range.
- Mode switch, comparer versus SQL order, highlight timing, log lines, interleaved scroll and reconcile, query plan uses the index, headless layout of log pane.

## Smoke test

At the top the rows change every 2 s with a blink and log lines. After scrolling down the visible rows stay still and the log reports browsing. Back at the top the head is refreshed. Resizing keeps 20 grid rows and a 10-row log.

## Implementation notes

- New types: `ChangeBatcher`, `ChangeBatch` (in `Changes/`), `IUiDispatcher` with `AvaloniaUiDispatcher` and `ImmediateUiDispatcher` (tests), new `IItemSort` members `NotBefore`, `NotAfter`, `Compare`.
- `GridViewModel` takes `TimeProvider` and `IUiDispatcher` as optional constructor parameters, so existing tests and plain construction still work; DI supplies both.
- `ItemsViewModel` subscribes to `ChangeBatcher.BatchReady` and forwards each batch through the dispatcher to `GridViewModel.ApplyChangesAsync`. The subscription is never removed because the page lives as long as the app.
- Viewport indices are stored in the view model and kept in step with every row insert or removal, so reconcile decisions (trim, mode) do not depend on a layout pass. `GridView` also queues a viewport check on every `Items` change so the indices are re-synchronized with what is really realized.
- The loaded-row dictionary (`loadedById`) is maintained only through the row helpers (`AppendRow`, `InsertRow`, `RemoveRowAt`, `ClearRows`); never mutate `Items` directly.
- The window assertion used by the tests ("the window equals the same range of a fresh query, without duplicates, with correct end flags") is the main correctness check; three seeded random batch sequences run it after every batch. Mutation checks (inverted comparer, dropped upper bound) make these tests fail, so they do detect errors.
- Event timing: the worker ticks every second and the batch window is 2 s, so a batch carries two to three ticks (12 to 18 events).
- Smoke results (real app): at the top, rows are inserted with a green blink and the log shows per-row lines; after scrolling down the log reports `browsing: head frozen` and `ignored N changes outside the loaded range`, and the visible rows do not move; scrolling back to the top shows the new head rows highlighted. UI stayed responsive.
