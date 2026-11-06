# LightSQL — Architecture

LightSQL is a lightweight WPF desktop SQL Server client targeting **.NET 10**
(`net10.0-windows`). It follows the **MVVM** pattern with constructor dependency
injection.

## Solution layout

```
run.cmd                          Builds (Debug) and starts the app; passes .sql file arguments on
src/LightSQL/
├─ LightSQL.slnx                 Solution (LightSQL + LightSQL.Tests)
├─ LightSQL/                     The WPF application
│  ├─ Models/                    Plain data types (DTOs, enums)
│  ├─ Services/                  Infrastructure (SQL access, storage, dialogs)
│  ├─ ViewModels/               View models (MVVM)
│  ├─ Views/                    Windows and user controls (XAML + code-behind)
│  ├─ Controls/                 Custom WPF controls/panels (ResultGridsPanel)
│  ├─ Behaviors/                Attached behaviours (TreeViewRightClickSelection)
│  ├─ Converters/               WPF value converters
│  ├─ Resources/                Shared XAML resources (ObjectExplorerIcons.xaml)
│  ├─ Themes/                   Controls.xaml (themed control styles) + Palettes/ (one per theme)
│  ├─ Editor/                   SqlSyntax.xshd (embedded T-SQL highlighting) + SqlHighlighting (theme colours)
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
Query tabs (`QueryTabViewModel`, each with its own `ResultsViewModel`) are created
by `MainViewModel` at runtime and are not registered in the container.

## Layers

- **Models** — no behaviour beyond small helpers. Enums start at `1`.
- **Services** — all I/O and SQL access sits behind interfaces:
  - `ISqlConnectionService` / `SqlConnectionService` — connect, execute batches,
    read metadata, create databases. Opens a fresh pooled connection per operation.
    `ExecuteBatchAsync(sql, database, token)` takes the target database explicitly
    (null = the connection's default, `CurrentDatabase`), so each query tab runs in
    its own database and several tabs can execute concurrently. There is no shared
    "current database" state to mutate. After a batch it compares
    `SqlConnection.Database` with the database it started in (read in a `finally`,
    so it also works when the batch failed) and reports a `USE` switch in
    `QueryExecutionResult.NewDatabaseContext` (static, unit-tested
    `GetNewDatabaseContext`).
  - `IConnectionStore` / `ConnectionStore` — saved connections (JSON + DPAPI).
    Passwords are stripped from raw connection strings before saving; when the
    profile saves its password, the typed or embedded password is stored encrypted
    in `ProtectedPassword` instead.
  - `IFileService` / `FileService` — reads and writes the `.sql` files open in
    tabs. `ReadText` returns a `TextFileContent` (text + detected encoding: byte
    order mark, else strict UTF-8, else the Windows ANSI code page); `WriteText`
    writes with a given encoding so files round-trip unchanged. `DefaultEncoding`
    (UTF-8 with BOM, like SSMS) is used for new files.
  - `IOptionsService` / `OptionsService` — application options (JSON; enums such
    as the theme are stored by name).
  - `IThemeService` / `ThemeService` — applies a colour theme by swapping the
    palette dictionary in the application resources; `ISystemThemeProvider` /
    `WindowsSystemThemeProvider` report the Windows light/dark mode for the Auto
    theme; `WindowTitleBar` makes title bars dark. See [themes.md](themes.md).
  - `ISqlStatementAnalyzer` / `SqlStatementAnalyzer` — UPDATE/DELETE-WHERE guard.
  - `ICredentialProtector` / `DpapiCredentialProtector` — DPAPI encrypt/decrypt.
  - `IDialogService` / `DialogService` — message boxes, text prompt, the Windows
    Open (multi-select) / Save As file dialogs, modal dialogs.
  - `ConnectionStringFactory` — builds connection strings from a profile.
- **ViewModels**
  - `MainViewModel` — connection, database list, options, and the collection of
    query tabs (`Tabs`, `SelectedTab`). Tab commands: `NewQuery`, `OpenFile`,
    `SaveFile`, `SaveFileAs` (both take an optional tab, default the selected one),
    `CloseQuery`, `CloseOtherQueries`. `OpenFiles(paths)` is also used by window
    drag-and-drop and command-line arguments (`App.OnStartup`). It remembers the
    folder of the last file opened/saved for the next dialog. `ExecuteQuery` and
    `Cancel` act on the selected tab; `ExecuteQuery` allows concurrent executions so
    another tab can run while one is busy. `ConfirmCloseAll()` is called by the
    window's `Closing` handler to ask about unsaved tabs. Implements `IMainActions`
    (`OpenQueryTab`, `ExecuteAsync`, `RefreshDatabaseListAsync`) for the Object
    Explorer without a circular dependency. The toolbar dropdown binds
    `SelectedItem` to `SelectedTab.ActiveDatabase`, so it follows the selected tab.
    A `ComboBox` cannot select a value that is not in its items and then keeps
    showing the previous selection, so `MainViewModel` watches every tab's
    `ActiveDatabase` and adds a missing name to `Databases` (sorted, matched
    case-insensitively; a different-case spelling is replaced by the listed one).
  - `QueryTabViewModel` — one query document: `EditorText`, `SelectedText`, caret,
    `ActiveDatabase`, `FilePath` + `FileEncoding` (null until the tab is opened from
    or saved to a file), `DefaultTitle`/`DisplayName` (file name)/`Header`/`ToolTip`
    (full path)/`SuggestedFileName`, `IsModified`, `IsSelected`, `IsExecuting`,
    status texts, its own `ResultsViewModel` and `CancellationTokenSource`. Contains
    the execution logic (UPDATE/DELETE guard, calling the SQL service, filling
    results, switching `ActiveDatabase` when the batch ran `USE`). `LoadText` sets generated text (no file) and `LoadFile` sets a file's
    text, both without marking the tab modified; `MarkSaved` records a save;
    `IsPristine` identifies an untouched new tab.
  - `ConnectionDialogViewModel` — the connection form and saved-connection list.
    Selecting a saved connection (`SelectedSavedConnection`) copies it into the form
    (`LoadSelected`) and raises `SavedConnectionLoaded`, which the dialog's
    code-behind uses to fill its `PasswordBox`es (passwords are never bound).
  - `ResultsViewModel`, `TreeNodeViewModel`, `ObjectExplorerViewModel`,
    `OptionsViewModel` support specific views. `OptionsViewModel` previews a
    chosen theme at once and `RevertThemePreview` (called when the dialog closes)
    restores the previous one unless the options were saved.
- **Views**
  - `MainWindow` — the shell. The query area is a `TabControl` re-templated to show
    only the tab headers (name, *(executing)* marker, ✕ button, context menu), and
    below it an `ItemsControl` with a `Grid` panel that holds **one `QueryTabView`
    per tab**, only the selected one visible. A plain `TabControl` with
    `ItemsSource` would reuse a single content view for all tabs and lose each
    editor's undo history, caret, scroll and splitter position; keeping one live
    view per tab avoids that.
  - `QueryTabView` — editor over Results/Messages for one tab; focuses its editor
    when it becomes visible.
  - `SqlEditorControl` — wraps AvalonEdit with bindable `DocumentText`,
    `SelectedText`, `CaretLine`, `CaretColumn`, `ShowLineNumbers`, `EditorFontSize`
    (the last two bind to `MainViewModel.ShowLineNumbers` / `EditorFontSize`, which
    are re-raised when Options are saved).
  - `ConnectionDialog`, `OptionsDialog`, `InputDialog`.
- **Controls** — `ResultGridsPanel` lays out the result grids: they fill the visible
  results area (one grid = whole pane, several share it equally), with a minimum
  height per grid, beyond which the panel grows and the surrounding `ScrollViewer`
  scrolls. Inside a ScrollViewer a panel is measured with infinite height, so the
  viewport height is bound in (`ViewportHeight`). Measuring every grid with a finite
  height also keeps DataGrid row virtualization working. The sizing rule is the
  static, unit-tested `CalculateItemHeight`.

- **Behaviors** — `TreeViewRightClickSelection.IsEnabled` (attached property, set
  on the Object Explorer `TreeView`) handles `PreviewMouseRightButtonDown`, walks
  up from the clicked element to the nearest `TreeViewItem` and selects and
  focuses it. WPF's `TreeView` only selects on a left-click; the event is left
  unhandled so the node's context menu still opens. Being an attached behaviour, it
  keeps this view-only concern out of the view models and code-behind and can be
  reused on any tree.

- **Object Explorer icons** — `Resources/ObjectExplorerIcons.xaml` (merged into
  `App.xaml`) holds one 16×16 vector `DrawingImage` per node kind (`Icon.Server`,
  `Icon.FolderClosed`, `Icon.FolderOpen`, `Icon.Database`, `Icon.Table`,
  `Icon.View`, `Icon.Column`, `Icon.Loading`, `Icon.Error`); vectors stay sharp at
  any DPI and need no image files. The tree's item template binds an `Image` to
  `ObjectType` + `IsExpanded` through `ObjectExplorerIconConverter`, whose static,
  unit-tested `GetIconKey` maps a node kind to its icon key (folders switch to the
  open icon while expanded). The "Loading..." and error child nodes have their own
  `DatabaseObjectType` values (`Placeholder`, `Error`) so they get their own icon
  and text style.

## Threading / responsiveness

All database calls are `async`. The Object Explorer loads each level lazily on
expand. Each query tab executes under its own `CancellationTokenSource`, so Cancel
aborts only that tab's query and tabs can run in parallel.

## Known limitations (v1)

- Each batch runs on its own pooled connection, so session state such as `#temp`
  tables and `SET` options does not persist between executions (not even within
  the same tab). `USE` is the exception: the tab adopts the database the batch
  switched to, and the next batch is opened in it. The toolbar **Database**
  dropdown also changes the selected tab's database.
- Open tabs are not restored after restarting the application.
- Dropping a database that has active/pooled connections fails with a SQL error
  (standard SQL Server behaviour); close other usage first.
- Object scripting ("Script as CREATE") is not implemented yet.
