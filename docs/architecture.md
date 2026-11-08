# LightSQL — Architecture

LightSQL is a lightweight WPF desktop SQL client targeting **.NET 10**
(`net10.0-windows`). It supports **Microsoft SQL Server**, **PostgreSQL** and
**MySQL / MariaDB** through a database-provider abstraction (see
[database-providers.md](database-providers.md)). It follows the **MVVM** pattern
with constructor dependency injection.

## Solution layout

```
run.cmd                          Builds (Debug) and starts the app; passes .sql file arguments on
src/LightSQL/
├─ LightSQL.slnx                 Solution (LightSQL + LightSQL.Tests)
├─ LightSQL/                     The WPF application
│  ├─ Models/                    Plain data types (DTOs, enums)
│  ├─ Services/                  Infrastructure (SQL access, storage, dialogs)
│  │  ├─ Database/               Per-engine providers + dialects + registry (SQL Server, PostgreSQL, MySQL)
│  │  ├─ Tools/                  Shared DB tool core (executor, catalog, assistant prompt) — used by MCP + chat
│  │  ├─ Llm/                    Chat LLM client (OpenAI-compatible), provider catalog, agent loop, client factory
│  │  └─ Mcp/                    MCP server for AI clients (host, tools, port finder)
│  ├─ ViewModels/               View models (MVVM)
│  ├─ Views/                    Windows and user controls (XAML + code-behind)
│  ├─ Controls/                 Custom WPF controls/panels (ResultGridsPanel, DocumentTabStyleSelector)
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
| Microsoft.Data.SqlClient | Microsoft SQL Server connectivity (`SqlServerDatabaseProvider`) |
| Npgsql | PostgreSQL connectivity (`PostgreSqlDatabaseProvider`) |
| MySqlConnector | MySQL / MariaDB connectivity (`MySqlDatabaseProvider`) |
| CommunityToolkit.Mvvm | `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]` |
| Microsoft.Extensions.DependencyInjection | Service / view-model wiring |
| ModelContextProtocol.AspNetCore | MCP server (official C# SDK) hosted in Kestrel; brings the `Microsoft.AspNetCore.App` shared framework |

Windows DPAPI (`System.Security.Cryptography.ProtectedData`) is provided by the
`net10.0-windows` framework and is used to encrypt saved passwords.

## Dependency injection

`App.OnStartup` builds a `ServiceProvider` (see `App.xaml.cs`). Services and the
main window are singletons; dialog view models are transient (a fresh instance per
dialog). `MainWindow` is resolved from the container, which injects `MainViewModel`.
Query tabs (`QueryTabViewModel`, each with its own `ResultsViewModel`) and MCP
server tabs (`McpServerTabViewModel`) are created by `MainViewModel` at runtime and
are not registered in the container. `Func<ISqlConnectionService>` is registered so
`McpServerHostFactory` can give every MCP server its own SQL service.
`IDatabaseProviderRegistry` (a stateless singleton holding one provider per engine)
is injected into `SqlConnectionService`, `ConnectionStore`, `ConnectionDialogViewModel`
and `McpServerHostFactory`.

## Layers

- **Models** — no behaviour beyond small helpers. Enums start at `1`.
- **Services** — all I/O and SQL access sits behind interfaces:
  - `Database/` — the multi-engine abstraction. `IDatabaseProvider` (connection
    creation, connection-string building, metadata, messages, error formatting) and
    `ISqlDialect` (pure SQL text: quoting, SELECT TOP/LIMIT, CREATE/DROP) have one
    implementation per engine (`SqlServer*`, `PostgreSql*`, `MySql*`);
    `IDatabaseProviderRegistry` / `DatabaseProviderRegistry` resolves the provider for
    a `ConnectionProfile.Engine`. See [database-providers.md](database-providers.md).
  - `ISqlConnectionService` / `SqlConnectionService` — connect, execute batches,
    read metadata, create databases, for any engine. It owns the connection lifetime
    over the ADO.NET base types (`DbConnection` / `DbCommand` / `DbDataReader` /
    `DbTransaction`) and delegates everything engine-specific to the active
    `IDatabaseProvider` (chosen from the profile's engine). It exposes `Dialect`,
    `AdministrativeDatabase` and `Engine`. Opens a fresh pooled connection per
    operation. `ExecuteBatchAsync(sql, database, token)` takes the target database
    explicitly (null = the connection's default, `CurrentDatabase`), so each query tab
    runs in its own database and several tabs can execute concurrently. There is no
    shared "current database" state to mutate. After a batch it compares
    `DbConnection.Database` with the database it started in (read in a `finally`, so it
    also works when the batch failed) and reports a `USE` switch in
    `QueryExecutionResult.NewDatabaseContext` (static, unit-tested
    `GetNewDatabaseContext`; PostgreSQL has no `USE`, so it never switches).
    `ExecuteReadOnlyBatchAsync` runs a batch in a transaction that is always rolled
    back (MCP read-only mode).
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
  - `ISqlStatementAnalyzer` / `SqlStatementAnalyzer` — UPDATE/DELETE-WHERE guard,
    and `FindWriteOperation` for the MCP server's read-only mode. A conservative,
    cross-engine scanner: it neutralizes `'literals'`, comments and
    `[bracketed]` / `"quoted"` / `` `backtick` `` identifiers before matching keywords.
  - `ICredentialProtector` / `DpapiCredentialProtector` — DPAPI encrypt/decrypt.
  - `IDialogService` / `DialogService` — message boxes, text prompt, the Windows
    Open (multi-select) / Save As file dialogs, modal dialogs.
  - Connection strings are built by each `IDatabaseProvider.BuildConnectionString`
    (there is no longer a shared `ConnectionStringFactory`).
  - `IClipboardService` / `ClipboardService` — copies text (MCP client setup).
  - `IMetadataStore` / `MetadataStore` — per connection + database metadata document
    (JSON, Markdown or any text) in `%APPDATA%\LightSQL\metadata.json`, sent to AI clients
    as extra MCP instructions.
  - `PromptLibrary` — reads embedded text prompts (the metadata-extraction prompt shown in
    Tools → Metadata Extraction Prompt).
  - `ILlmSettingsStore` / `LlmSettingsStore` — the chat's LLM settings in `llm.json`: one API key
    per provider (each DPAPI-encrypted, never plaintext) and the last-used provider/model.
  - `Tools/` — the transport-neutral DB tool core: `DatabaseToolExecutor` (list/describe/execute
    with the single-database lock + read-only guard), `DatabaseToolCatalog` (tool schema) and
    `DatabaseAssistantPrompt` (shared system prompt). Used by both the MCP server and the chat.
  - `Llm/` — `ILlmChatClient` / `OpenAiCompatibleChatClient` (the LLM backend, used by every
    provider), `LlmProviderCatalog` (the preconfigured providers + suggested models), `ChatAgent`
    (the tool-call loop, with a swappable client) and `ILlmClientFactory` / `LlmClientFactory`
    (builds a client for a chosen provider + model). See [chat.md](chat.md).
  - `Mcp/` — `IMcpServerHostFactory` / `McpServerHostFactory` start an MCP server
    (own `SqlConnectionService`, loopback port 9000+, stateless Streamable HTTP at
    `/mcp`, Host/Origin guard); `McpDatabaseTools` (the tools, each locked to the
    server's single database), `McpResultFormatter` (JSON), `McpPortFinder`,
    `McpRequestGuard`. See [mcp-server.md](mcp-server.md).
- **ViewModels**
  - `DocumentTabViewModel` — abstract base of every tab in the document strip
    (`Header`, `ToolTip`, `DisplayName`, `IsSelected`, `OnClosed()` called after the
    tab is removed).
  - `MainViewModel` — connection, database list, options, and the collection of
    document tabs (`Tabs`, `SelectedTab`, both typed `DocumentTabViewModel`).
    `QueryTabs` / `McpServerTabs` filter them by type and `SelectedQueryTab` is the
    selected tab when it is a query tab (null for an MCP tab); the toolbar,
    status bar, Execute, Cancel and Save act on `SelectedQueryTab`.
    `StartMcpServer` / `OpenMcpServerTabAsync(database)` open an MCP server tab (locked
    to the selected database) for the connection profile remembered at Connect;
    `EditDatabaseMetadata` opens the metadata dialog; `StopAllMcpServersAsync` runs
    when the window closes. Disconnect leaves MCP servers running. Tab commands: `NewQuery`, `OpenFile`,
    `SaveFile`, `SaveFileAs` (both take an optional tab, default the selected one),
    `CloseQuery`, `CloseOtherQueries` (both take any tab; only query tabs prompt to save). `OpenFiles(paths)` is also used by window
    drag-and-drop and command-line arguments (`App.OnStartup`). It remembers the
    folder of the last file opened/saved for the next dialog. `ExecuteQuery` and
    `Cancel` act on the selected tab; `ExecuteQuery` allows concurrent executions so
    another tab can run while one is busy. `ConfirmCloseAll()` is called by the
    window's `Closing` handler to ask about unsaved tabs. Implements `IMainActions`
    (`OpenQueryTab`, `ExecuteAsync`, `RefreshDatabaseListAsync`,
    `OpenMcpServerTabAsync`) for the Object Explorer without a circular dependency. The toolbar dropdown binds
    `SelectedItem` to `SelectedQueryTab.ActiveDatabase`, so it follows the selected tab.
    A `ComboBox` cannot select a value that is not in its items and then keeps
    showing the previous selection, so `MainViewModel` watches every tab's
    `ActiveDatabase` and adds a missing name to `Databases` (sorted, matched
    case-insensitively; a different-case spelling is replaced by the listed one).
  - `ChatTabViewModel` — one chat bound to a database: provider + model pickers, transcript, input,
    Send/Stop, the write-access toggle; runs tool calls through the shared executor and the
    `ChatAgent` loop.
  - `McpServerTabViewModel` — one MCP server: state, port, endpoint, client setup
    text and copy commands, activity log (tool calls posted to the UI thread),
    access mode (read-only by default, `IsReadWrite` after confirmation).
    Closing the tab stops the server.
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
    The **Database type** picker (`Engines` / `SelectedEngine`) drives which fields
    show: `ShowAuthenticationType` (SQL Server only) and `AreCredentialsEnabled`
    (always for PostgreSQL / MySQL; under SQL auth for SQL Server). It carries the
    engine and optional port into the profile. Selecting a saved connection
    (`SelectedSavedConnection`) copies it into the form (`LoadSelected`) and raises
    `SavedConnectionLoaded`, which the dialog's code-behind uses to fill its
    `PasswordBox`es (passwords are never bound).
  - `ResultsViewModel`, `TreeNodeViewModel`, `ObjectExplorerViewModel`,
    `OptionsViewModel` support specific views. `OptionsViewModel` previews a
    chosen theme at once and `RevertThemePreview` (called when the dialog closes)
    restores the previous one unless the options were saved.
- **Views**
  - `MainWindow` — the shell. The document area is a `TabControl` re-templated to
    show only the tab headers, and below it an `ItemsControl` with a `Grid` panel
    that holds **one view per tab** (`QueryTabView`, `McpServerTabView` or `ChatTabView`, chosen by
    implicit `DataTemplate`s per view-model type), only the selected one visible.
    Header templates are also per type (query: name, *(executing)*, ✕; MCP: state
    dot, name with port, ✕), and `DocumentTabStyleSelector` gives each type its own
    `TabItem` style and context menu. A plain `TabControl` with
    `ItemsSource` would reuse a single content view for all tabs and lose each
    editor's undo history, caret, scroll and splitter position; keeping one live
    view per tab avoids that.
  - `QueryTabView` — editor over Results/Messages for one tab; focuses its editor
    when it becomes visible.
  - `SqlEditorControl` — wraps AvalonEdit with bindable `DocumentText`,
    `SelectedText`, `CaretLine`, `CaretColumn`, `ShowLineNumbers`, `EditorFontSize`
    (the last two bind to `MainViewModel.ShowLineNumbers` / `EditorFontSize`, which
    are re-raised when Options are saved).
  - `McpServerTabView` — MCP server details, client setup and activity grid.
  - `ChatTabView` — the chat transcript, access toggle and input box.
  - `ConnectionDialog`, `OptionsDialog`, `MetadataDialog`, `MetadataPromptDialog`,
    `LlmSettingsDialog`, `InputDialog`.
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
- Dropping a database that has active/pooled connections fails with a SQL error;
  close other usage first. (PostgreSQL also cannot drop the database you are connected
  to — drops run from the engine's administrative database, e.g. `postgres`.)
- The SQL editor's syntax highlighting is tuned for T-SQL; PostgreSQL- and
  MySQL-specific keywords may not all be highlighted, but queries still run.
- Object scripting ("Script as CREATE") is not implemented yet.
