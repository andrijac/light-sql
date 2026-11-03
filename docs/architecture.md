# LightSQL — Architecture

LightSQL is a lightweight WPF desktop SQL Server client targeting **.NET 10**
(`net10.0-windows`). It follows the **MVVM** pattern with constructor dependency
injection.

## Solution layout

```
src/LightSQL/
├─ LightSQL.slnx                 Solution (LightSQL + LightSQL.Tests)
├─ LightSQL/                     The WPF application
│  ├─ Models/                    Plain data types (DTOs, enums)
│  ├─ Services/                  Infrastructure (SQL access, storage, dialogs)
│  ├─ ViewModels/               View models (MVVM)
│  ├─ Views/                    Windows and user controls (XAML + code-behind)
│  ├─ Converters/               WPF value converters
│  ├─ Editor/                   SqlSyntax.xshd (embedded T-SQL highlighting)
│  ├─ App.xaml(.cs)             Entry point + DI container
│  └─ MainWindow.xaml(.cs)      The shell window
└─ LightSQL.Tests/              xUnit unit tests
```

## Dependencies

| Package | Role |
|---|---|
| AvalonEdit | SQL editor: syntax highlighting, line numbers, caret tracking |
| Microsoft.Data.SqlClient | SQL Server connectivity |
| CommunityToolkit.Mvvm | `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]` |
| Microsoft.Extensions.DependencyInjection | Service / view-model wiring |

Windows DPAPI (`System.Security.Cryptography.ProtectedData`) is provided by the
`net10.0-windows` framework and is used to encrypt saved passwords.

## Dependency injection

`App.OnStartup` builds a `ServiceProvider` (see `App.xaml.cs`). Services and the
main window are singletons; dialog view models are transient (a fresh instance per
dialog). `MainWindow` is resolved from the container, which injects `MainViewModel`.

## Layers

- **Models** — no behaviour beyond small helpers. Enums start at `1`.
- **Services** — all I/O and SQL access sits behind interfaces:
  - `ISqlConnectionService` / `SqlConnectionService` — connect, execute batches,
    read metadata, create databases. Opens a fresh pooled connection per operation
    targeting the active database (keeps execution thread-safe and the UI responsive).
  - `IConnectionStore` / `ConnectionStore` — saved connections (JSON + DPAPI).
  - `IQueryStore` / `QueryStore` — named `.sql` files in the user profile.
  - `IOptionsService` / `OptionsService` — application options (JSON).
  - `ISqlStatementAnalyzer` / `SqlStatementAnalyzer` — UPDATE/DELETE-WHERE guard.
  - `ICredentialProtector` / `DpapiCredentialProtector` — DPAPI encrypt/decrypt.
  - `IDialogService` / `DialogService` — message boxes, text prompt, modal dialogs.
  - `ConnectionStringFactory` — builds connection strings from a profile.
- **ViewModels** — `MainViewModel` orchestrates everything and implements
  `IMainActions` so the `ObjectExplorerViewModel` can drive the editor and database
  list without a circular dependency. `ResultsViewModel`, `TreeNodeViewModel`,
  `ConnectionDialogViewModel`, `OptionsViewModel` support specific views.
- **Views** — `MainWindow`, `SqlEditorControl` (wraps AvalonEdit with bindable
  `DocumentText`, `SelectedText`, `CaretLine`, `CaretColumn`), `ConnectionDialog`,
  `OptionsDialog`, `InputDialog`.

## Threading / responsiveness

All database calls are `async`. The Object Explorer loads each level lazily on
expand. Query execution runs under a `CancellationTokenSource` so the Cancel button
can abort a long query.

## Known limitations (v1)

- Each batch runs on its own pooled connection, so session state such as `USE`,
  `#temp` tables and `SET` options does not persist between executions. Use the
  toolbar **Database** dropdown to change the active database.
- Dropping a database that has active/pooled connections fails with a SQL error
  (standard SQL Server behaviour); close other usage first.
- Object scripting ("Script as CREATE") is not implemented yet.
