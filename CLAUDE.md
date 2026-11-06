# CLAUDE.md

## Project

Desktop application built with .NET 10, Avalonia 12 and CommunityToolkit.Mvvm (MVVM pattern).

## Code style

- Follow the general Microsoft C# coding conventions and .NET design guidelines.
- Prefer clear names over explanatory comments. Comment only where intent is not obvious from the code (non-trivial reasoning, workarounds, constraints).
- Do not add summary or XML doc comments by default.
- Use CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`) instead of hand-written boilerplate.

## Workflow

Work on one feature or bugfix at a time, in sequence. Never work on multiple in parallel.

Implement features in complete chunks. A chunk is finished only when the Definition of Done is met.

Upcoming features may break or refactor previous features as needed. When that happens, update the affected tests, design documents and `README.md` in the same branch.

### Definition of Done

1. Design is clear and no open questions remain. Ask the user to resolve any open question before implementing.
2. Code builds without errors.
3. Tests are added for the new behavior, and all tests (new and existing) pass.
4. Smoke test verification is done, if the change touches UI. Run the app and confirm the change works.
5. `README.md` is updated to reflect the change.
6. Changes are merged into `main`.

### Git flow

Use git with the local repository only. Do not push.

1. If an `origin` remote is present, update `main` from it first. Otherwise skip this step.
2. Create a branch off `main`: `feature/<name>` for features, `bugfix/<name>` for bugfixes.
3. Implement the work and commit to the branch in one or more commits. Each commit must be self-contained (builds and passes tests).
4. When the work is done, perform a fresh review of the full branch diff. Fix any issues found.
5. Squash merge the branch into `main`. The squash commit message describes all changes made in the branch.

## Documentation

All decisions and design must be documented in the repository (for example in `docs/` or in `README.md`), including the reasoning for non-obvious choices. Update the documentation in the same branch as the change it describes.
