# LightSQL — Coding Standards

These restate the conventions in `CLAUDE.md` that the codebase follows.

## General

- WPF + MVVM; comment code and keep it readable.
- Keep `docs/` up to date and write unit tests for new functionality.
- One class per file.

## C# style

- Every block statement (`if`, `for`, `while`, `foreach`, etc.) uses curly braces.
- Blank line around block statements.
- Private fields start with a lowercase letter.
- Prefer explicit boolean comparisons for readability, e.g. `isNew == true`.
- Enum values start at `1` (never `0`) so an unset default is never a valid value.
- EOF-newline warnings are ignored (treated as noise on Windows).

## Member ordering

Order members by type, then by access modifier within each group.

1. Fields
2. Constants
3. Properties
4. Indexers
5. Constructors
6. Destructors / Finalizers
7. Methods
8. Operators
9. Events
10. Nested types

Access order within a group: `public` → `internal` → `protected internal` →
`protected` → `private protected` → `private`.

## Testing

- Unit tests use xUnit in `LightSQL.Tests`.
- Pure logic (the UPDATE/DELETE guard, credential protection, connection-string
  building, stores) is unit-tested without a live server.
- The SQL service is verified manually / via a temporary LocalDB smoke test against
  `(localdb)\MSSQLLocalDB` (connect, list, execute, create, metadata, drop). That
  smoke test is not part of the permanent suite.

Run tests:

```bash
dotnet test src/LightSQL/LightSQL.slnx
```
