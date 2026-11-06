# TestGrid

Desktop app (.NET 10, Avalonia 12, CommunityToolkit.Mvvm) that shows items from a local SQLite database in a custom `GridView` component.

Design documents live in [`design/`](design/). Working rules are in [`CLAUDE.md`](CLAUDE.md).

## Layout

- `src/TestGrid` - the application (single project).
- `tests/TestGrid.Tests` - xUnit tests (in-memory SQLite).

## Build, run, test

```
dotnet build
dotnet run --project src/TestGrid
dotnet test   # xunit v3 on Microsoft.Testing.Platform, see global.json
```

The database is created at `%LocalAppData%/TestGrid/items.db`. On start the app applies migrations and seeds 1000 rows if the table is empty. Delete the file to reseed.

## Migrations

```
dotnet ef migrations add <Name> --project src/TestGrid --output-dir Data/Migrations
```

Requires `dotnet tool install -g dotnet-ef`.

## Features

- Initial: Items page with a grid of all items - [design](design/initial.md).
- Filter and sort: hardcoded filter (Quantity > 10, Name starts with "pc part") and sort (ModifiedOn desc, Id asc) applied in the DB query; shown above the grid - [design](design/feature-1-filter-sort.md). Delete the existing database to reseed with the 50% matching distribution.
- Infinite scroll: the grid loads 20 rows at a time by cursor on (ModifiedOn desc, Id asc), trims rows far from the viewport and reloads them on scroll back - [design](design/feature-2-infinite-scroll.md).
