# Feature 3: Background data changes

## Goal

A background service changes the data continuously and publishes an event for every change. Nothing subscribes to the event yet.

## Behavior

Every second the service performs:

- N inserts of new random items,
- N updates of random existing items,
- N deletes of random existing items.

`N` is defined in one place: `DataChangeOptions.ChangesPerType` (default 4).

All writes go through `IDbContextFactory<AppDbContext>`, so `ModifiedOnInterceptor` applies to inserts and updates.

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

Run the app; the database changes about 12 rows per second (visible through logs at `Information` level), and the UI does not break.
