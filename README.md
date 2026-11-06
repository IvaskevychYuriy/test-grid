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
dotnet test
```

The database is created at `%LocalAppData%/TestGrid/items.db`. On start the app applies migrations and seeds 1000 rows if the table is empty. Delete the file to reseed.

## Migrations

```
dotnet ef migrations add <Name> --project src/TestGrid --output-dir Data/Migrations
```

Requires `dotnet tool install -g dotnet-ef`.

## Features

- Initial: Items page with a grid of all items - [design](design/initial.md).
