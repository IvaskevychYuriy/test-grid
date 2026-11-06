# Feature 1: Filter and sort

## Goal

The Items page defines a hardcoded filter and a hardcoded total sort order, passes them to `GridView`, and `GridView` applies them in the database query. For now it still shows all queried rows.

## Conditions

- Filter: `Quantity > 10` AND `Name` starts with `"pc part"`.
- Sort: `ModifiedOn` descending, then `Id` ascending. This is a total order because `Id` is unique.

## Design

- `ItemsViewModel` creates the filter and sort and exposes them to the view.
- `GridView` gets them as bindable properties and hands them to `GridViewModel`, which applies them in the EF query (translated to SQL, not filtered in memory).
- The page shows a label above the grid with a hardcoded text describing the filter and sort.

## Seed data

The generator makes 50% of rows match the filter (`Name` starts with `"pc part"`, `Quantity > 10`). The other half does not match (different name prefix and/or `Quantity <= 10`, including boundary values like exactly 10). Seeding still runs only on an empty database, so an existing dev database must be deleted to get the new distribution.

## Testing

- Query returns only matching rows, in the specified order, including ties on `ModifiedOn`.
- Seeder produces about 50% matching rows.

## Smoke test

The grid shows only rows with a `pc part` name and quantity above 10; the description label is visible.

## Implementation notes

- `GridViewModel.Filter` is an `Expression<Func<Item, bool>>` and `GridViewModel.Sort` is a `Func<IQueryable<Item>, IOrderedQueryable<Item>>`. `ItemsViewModel` sets them on the `GridViewModel` it owns; the `GridView` binds to that view model, so this is how the conditions reach the component.
- Seed constants (`"pc part"`, quantity threshold 10) live in `PcPartCriteria`, shared by the page and the seeder.
- `StartsWith` becomes SQL `LIKE 'pc part%'`, which SQLite evaluates case-insensitively for ASCII. Generated names never differ from the prefix only by case, so this does not affect the results.
- Feature 2 needs cursor conditions derived from the sort. The delegate-based sort is expected to be replaced by a structured sort definition then.
