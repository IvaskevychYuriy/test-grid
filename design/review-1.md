# Review 1: draft design vs `GridViewModel.ReconcileAsync`

Compares the draft design artifact "Live feed + virtual scroll with after-image-only CDC events" (https://claude.ai/artifact/QssgstBRjJ7MgcvnjtLj33, revision as of 2026-11-06) with `GridViewModel.ReconcileAsync` and the helpers it calls.

**Verdict:** the implementation follows the draft's event-handling table closely. It differs from the draft in three ways: it batches the in-range lookups, it backfills after removals, and it trims during reconcile. One real gap was found: sort-key changes were detected with a hard-coded `ModifiedOn` check. That gap is fixed in the same branch as this review.

## Matches the draft

| Draft | Implementation |
| --- | --- |
| In range = matches F and `(atHead or key >= lo) and (atTail or key <= hi)` | `lowerBound` is set only when `IsLowerBounded` (`HasMoreBefore` or `IsHeadFrozen`), `upperBound` only when `HasMoreAfter`. The DB query applies them with `Sort.NotBefore` / `Sort.NotAfter`. |
| F is never re-implemented in code | `QueryInRangeAsync` applies the filter inside the SQL query (`FilteredItems`). |
| Upsert, loaded, in range: patch, or reorder if the sort key changed | `ApplyUpdate` patches in place or removes and re-inserts at `FindInsertIndex`. |
| Upsert, loaded, not in range: remove | Row removed, logged as "left the loaded range or the filter". |
| Upsert, not loaded, in range: insert at its position | Inserted at `FindInsertIndex`. |
| Upsert, not loaded, not in range: nothing | Counted as ignored. |
| Delete, loaded: remove / not loaded: nothing | Same; not-loaded deletes are counted as ignored. |
| Leaving the top: set the lower bound to the first row's key (option 1) | `IsHeadFrozen` makes `lowerBound = Items[0].Cursor`, so new rows above the window are ignored. |
| Live mode slightly below the top: switch to browsing as soon as they leave the top | The head freezes as soon as `firstVisible > 0`. |
| Reaching the top: refetch the head and re-enter live mode | `RefreshHeadAsync`, applied as a diff by id. |
| Trimming clears `atHead` / `atTail` | `TrimToCapacity` sets `HasMoreBefore` / `HasMoreAfter`. |

## Differs from the draft

1. **Backfill.** The draft says "No backfill. Removing a row just shortens the list." `ReconcileAsync` calls `RefillAsync`, which fetches rows after `hi` (then before `lo` once the tail is reached) to restore the window size. An empty window is reloaded from the head. This is intentional: the feature 4 requirement is that a delete inside the viewed window fetches replacement rows ([feature 4](feature-4-live-updates.md), "Deviation from the doc"). It costs at most two `LIMIT` cursor queries per batch.
2. **One lookup per batch, not per event.** The draft says "one primary-key lookup per event". The implementation aggregates events into a `ChangeBatch` (net effect per id) and runs one `Id IN (...)` query per 500 ids. The meaning is the same and it needs fewer round trips.
3. **Trimming during reconcile.** The draft trims only while navigating. The implementation also calls `TrimToCapacity` after inserts, so a burst of inserts cannot grow the window past `MaxLoadedRows`.
4. **No "new items at top" flag.** The draft suggests an optional flag and a "jump to latest" control while browsing. The implementation drops head inserts and only logs them as ignored. Scrolling back to the top refreshes the head.
5. **Per-viewer server state: not applicable.** The app reconciles on the client. `loadedById` plays the role of the draft's "loaded ids", so removals of unknown ids need no server-side tracking.

## Gap found and fixed: hard-coded sort key

**Before:** `ApplyUpdate` detected a key change with `row.ModifiedOn != item.ModifiedOn`. Inserts built their position with `new ItemCursor(item.ModifiedOn, item.Id)`, and `ItemViewModel.Cursor` was computed from `ModifiedOn`. `GridViewModel.Sort` is a pluggable `IItemSort`, and every other path already went through it. With any other sort, a change to the sort column was patched in place without moving the row, and inserts were placed by the wrong key.

**Fix:**

- `IItemSort.CursorOf(Item)` builds the cursor of a row; `ModifiedOnDescendingIdAscendingSort` returns `(ModifiedOn, Id)`.
- `ItemViewModel` receives its cursor from the grid (constructor and `ApplyFrom`) instead of computing it.
- `GridViewModel` creates rows through `CreateRow`, which asks `Sort` for the cursor. `ApplyUpdate` detects a key change with `Sort.Compare(row.Cursor, newCursor) != 0`.
- Test `UpdateChangingANonModifiedOnSortKeyMovesTheRow` uses the test-only `QuantityDescendingIdAscendingSort`: changing `Quantity` without touching `ModifiedOn` moves the row to the top. With the old check the test fails.

**Remaining limit (by decision):** `ItemCursor` keeps its fixed `(DateTimeOffset ModifiedOn, Guid Id)` shape. A production sort on another column needs `ItemCursor` generalized first. The grid needs no further change for that. The test sort stores `Quantity` in the `ModifiedOn` slot as seconds since the Unix epoch to work within this limit.
