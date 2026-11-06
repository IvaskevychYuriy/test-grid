# Feature 3: Background data changes

## Goal

A background service changes the data continuously and publishes an event for every change. Nothing subscribes to the event yet.

## Behavior

Every second the service performs:

- N inserts of new random items,
- N updates of random existing items,
- N deletes of random existing items.

`N` is defined in one place: `DataChangeOptions.ChangesPerType` (default 4).

All writes go through `IDbContextFactory<AppDbContext>`. `ModifiedOn` is set explicitly by the worker (no interceptor): inserts always get `UtcNow`, and each update randomly either sets `ModifiedOn = UtcNow` or leaves it unchanged.

## Event

`IDataChangeNotifier` (singleton) exposes `event EventHandler<DataChangedEventArgs> DataChanged`. It is raised once per changed row.

`DataChangedEventArgs`:

- `ChangeType`: `Insert`, `Update` or `Delete`.
- `ItemId`: the changed item.
- `Patch`: for insert and update, the changed values (the full set of fields for insert; the fields that changed, including `ModifiedOn`, for update). `null` for delete.

## Design

- `BackgroundDataChanger : BackgroundService` with a one second `PeriodicTimer`.
- The tick logic lives in a separately callable method so tests can run one tick without waiting.
- The service is registered as a hosted service only.

## Testing

- One tick produces the configured number of inserts, updates and deletes, and the database changes accordingly.
- Events carry the correct type and patch.
- Changing `ChangesPerType` changes the counts.

## Smoke test

Run the app with `Logging__LogLevel__Default=Debug`; it logs one applied tick per second (12 row changes), no SQL is logged, and the UI does not break.

## Implementation notes

- Classes live in `src/TestGrid/Changes`: `DataChangeOptions` (`ChangesPerType = 4`, `Interval = 1 s`, the only place to change them), `DataChangeGenerator` (one tick), `BackgroundDataChanger` (hosted timer loop), `DataChangeNotifier` / `IDataChangeNotifier` (event), `DataChangedEventArgs`, `ItemPatch`, `DataChangeType`.
- `ItemPatch` has nullable `Name`, `Quantity`, `Price` and `ModifiedOn`. Insert patches carry all four. Update patches carry only the fields that were changed (at least one of name, quantity, price; each random) plus `ModifiedOn` only when the update changed it (null otherwise). Delete events have no patch.
- One tick reads `2 x ChangesPerType` random rows (`EF.Functions.Random()`): the first half is updated, the second half is deleted, so no row is updated and deleted in the same tick. Inserts reuse `DbSeeder.GenerateItems`, so about half of them match the feature 1 filter. With fewer rows than requested the counts shrink (an empty table only gets inserts).
- All changes of a tick go in one `SaveChanges`. Events are published after it succeeds, one per row, on the background thread. A future subscriber that touches UI must marshal to the UI thread.
- The host is now started in `Program.Main` after migration and seeding (`host.Start()`), and stopped when the window closes. Before this feature nothing needed the host to run.
- A failing tick is logged and the loop continues. The per-tick log line is at `Debug` level; run with `Logging__LogLevel__Default=Debug` to see it.
- Smoke result: with debug logging the app logged one tick per second, no EF commands were logged, and the UI stayed responsive.
- Known interaction: the grid does not react to the changes yet. Updates move rows to the top of the sort order, so while scrolling, the cursor can load rows in a different place than a fresh query would, and deleted rows stay visible until reloaded. Cursors are values, not row references, so paging itself never fails. A later feature binds the event to the grid.
- Updates that keep `ModifiedOn` do not move in the sort order; updates that set it jump to the top. Both cases are intended, so consumers handle in-place patches and moves.
