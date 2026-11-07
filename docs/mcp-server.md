# LightSQL — MCP Server (AI clients)

LightSQL can expose a connection as an **MCP server** (Model Context Protocol), so
AI clients such as **Claude Code**, **Claude Desktop** or **Codex** can explore the
database and run queries from a chat.

## Starting a server

You must be connected first. Each MCP server is **locked to a single database** (see
[Single-database lock](#single-database-lock)), so you start one for a specific database:

- The **Start MCP** toolbar button, right next to the database dropdown: starts a server
  for the database selected in that dropdown. It is disabled until a database is selected.
- **Database → Start MCP Server**: the same — uses the selected database.
- Right-click a **database node** in the **Object Explorer → Start MCP Server**. This menu
  item appears **only on a database node** — not on the server node, the Tables/Views
  folders, or individual tables, views or columns.

Every server starts **read-only** (see [Access mode](#access-mode)).

Each server opens in its **own tab** in the same strip as the query tabs, e.g.
`● MCP SQL01 :9000`. The dot is green while it runs, grey when stopped and red
when it failed. Every server gets the **first free port from 9000 up** (9000,
9001, ...), so several servers can run at once, for the same or different
databases.

## The MCP server tab

| Section | Content |
|---|---|
| Header | Server name, **Start Server** / **Stop Server** buttons |
| Server | Status, engine and server/host, default database, how it signs in (Windows / login name; never the password), port, endpoint URL (with **Copy**), transport, start time, number of tool calls, **Access** (the *Allow write-access* check box and the current mode) |
| Connect an AI client | The ready-to-run Claude Code command and the Codex `config.toml` snippet, each with **Copy**; the tool list; a reminder that query results go to the AI provider |
| Activity | One row per tool call, newest first: time, tool, database, SQL / argument (full text in the tooltip), result (errors in red), duration. **Clear** empties the log (the counter keeps counting). At most 500 rows are kept. |

Right-click the tab header for **Start Server**, **Stop Server**, **Copy Endpoint
URL**, **Close** and **Close All But This**. Closing the tab (✕, middle-click,
Ctrl+F4) **stops the server**. A stopped server can be started again from its tab;
it may then get a different port. Closing LightSQL stops all servers.

**Disconnect** does not stop running servers: each server has its own connection.

## Access mode

| Mode | What AI clients can do |
|---|---|
| **Read-only** (default) | Only batches that read: `SELECT` / `WITH`, `DECLARE` / `SET` variables, `IF` / `WHILE` / `BEGIN`…`END`, `PRINT`, `USE`. Anything else is **rejected before it runs**, and accepted batches still run in a **transaction that is always rolled back**. |
| **Write-access** | Any SQL in the connected engine's dialect: `INSERT`, `UPDATE`, `DELETE`, `MERGE`, DDL, `EXEC`/`CALL`, … with the permissions of the connection's login. |

- Switch with the **Allow write-access (AI can change data)** check box in the tab.
  Turning it **on** asks for confirmation; turning it off does not. The change
  applies to the **next query**, without restarting the server, and is kept when
  the server is stopped and started again from the same tab. A new tab is always
  read-only.
- In write-access mode the tab header shows `[write-access]` and the access text is red.
- **Rejected statements** (keywords found outside strings, comments and
  `[bracketed]` / `"quoted"` / `` `backtick` `` names): `INSERT`, `UPDATE`, `DELETE`,
  `MERGE`, `TRUNCATE`, `INTO` (`SELECT … INTO`), `CREATE`, `ALTER`, `DROP`, `ENABLE`,
  `DISABLE`, `GRANT`, `REVOKE`, `DENY`, `EXEC`/`EXECUTE`, `COMMIT`, `ROLLBACK`,
  `SAVE`, `TRAN`, `TRANSACTION`, `BACKUP`, `RESTORE`, `DBCC`, `BULK`, `CHECKPOINT`,
  `KILL`, `SHUTDOWN`, `RECONFIGURE`, `SETUSER`, `WRITETEXT`, `UPDATETEXT`,
  `OPENROWSET`, `OPENQUERY`, `OPENDATASOURCE`, `SEND`, `RECEIVE`, and the cross-engine
  `COPY`, `CALL`, `LOCK`, `VACUUM`, `LOAD`, `RENAME`; also a batch that starts with a
  name, which T-SQL runs as a procedure without `EXEC` (`sp_configure 'x', 1`).
  The check errs on the side of blocking: an unquoted column named like a keyword
  (e.g. `Update`) is rejected — quote it. Stored procedures and table variables /
  `#temp` inserts need write-access.
- The AI receives an error such as *"Rejected: this MCP server is read-only and
  the batch contains INSERT … The user can grant write-access in
  LightSQL"*; the activity log shows **Blocked (read-only): INSERT**.
- The rollback is the safety net behind the check: whatever a batch changes in a
  read-only call is undone. Not covered by a rollback: sequence values taken by
  `NEXT VALUE FOR` and identity values are not reused (no data is kept).

## Connecting an AI client

The endpoint is `http://127.0.0.1:<port>/mcp` (Streamable HTTP transport). LightSQL
suggests the client name **`LightSQL-<database>`** (the database the server is locked to,
e.g. `LightSQL-Sales`; the name is sanitized to a valid key, and falls back to the port
when there is no database).

**Claude Code** (run in a terminal; the tab's **Copy** button gives the exact line):

```bash
claude mcp add --transport http LightSQL-Sales http://127.0.0.1:9000/mcp
```

**Codex** — add to `~/.codex/config.toml`:

```toml
[mcp_servers.LightSQL-Sales]
url = "http://127.0.0.1:9000/mcp"
```

Any other client that supports the Streamable HTTP transport works the same way.
If the server is restarted on another port, update the client's URL.

## Tools

| Tool | Arguments | Returns |
|---|---|---|
| `list_databases` | – | JSON array naming the one database the server is locked to |
| `list_tables` | `database?` | `{database, objects:[{schema, name, type: table/view}]}` |
| `describe_table` | `table` (`Orders`, `sales.Orders`, `[sales].[Order Details]`), `schema?`, `database?` | `{database, schema, name, columns:[{name, type, nullable}]}` |
| `execute_query` | `sql`, `database?`, `maxRows?` (default 200, max 5000); read-only unless allowed (see Access mode) | `{success, database, elapsedMs, recordsAffected, messages, resultSets:[{columns:[{name,type}], rows:[[...]], totalRows, truncated}]}` |

- The server is **locked to one database** (see below): omit the `database` argument, or
  set it to the server's database. Any other database is rejected before the call runs.
- Values: NULL → `null`, numbers/booleans native, dates ISO 8601, binary `0x...`,
  text longer than 4000 characters is cut with `...`.
- A failed batch (SQL error) returns an MCP *error result* whose text is the same
  JSON, including the server's error messages, so the AI can correct its query.
- Each call runs on a **fresh pooled connection**, like the query tabs: temporary
  tables, session settings and `USE` do not carry over to the next call (the result
  says so when a batch ran `USE`; PostgreSQL has no `USE`).
- The read-only tools are annotated `readOnly`; `execute_query` is annotated
  `destructive` (annotations are fixed, while the mode can change at runtime), so
  clients that honour annotations ask before running it.
- On connect, the client receives *instructions* naming the engine, the server, the
  locked database, the read-only default and the rules above.

## Single-database lock

Every MCP server serves exactly one database — the one you started it for:

- `list_databases` returns only that database.
- `list_tables`, `describe_table` and `execute_query` cannot reach another database: omit
  the `database` argument (or pass the server's own database). Naming a different one
  returns an error such as *"Rejected: this MCP server is locked to database 'Sales' and
  cannot access 'HR'."*, and the activity log shows **Blocked (locked database)**.

To work with another database, start a second server for it; each gets its own tab and port.

## Database metadata (extra instructions for the AI)

You can give the AI more than the live catalog exposes — primary and foreign keys,
relationships, enum meanings, units, and which filters to apply — by pasting a JSON
metadata document for a connection and database. LightSQL ships a prompt that generates
that JSON from a code repository under **Tools → Metadata Extraction Prompt…** (with a
**Copy** button). See [mcp-metadata.md](mcp-metadata.md) for the full workflow.

- Open **Database → Database Metadata…**, choose the connection and database, paste the
  JSON and **Save**. It is stored per connection + database in
  `%APPDATA%\LightSQL\metadata.json`.
- When a server starts for that connection and database, the JSON is appended to the
  server's **instructions** (the AI's "system prompt"), so the AI has it while writing
  queries. Editing the metadata applies the next time the server starts (restart a running
  server to pick it up).
- The metadata is **sent to the AI client and its model provider**, so do not put
  passwords, tokens or connection strings in it.

## Security

- The server listens on **127.0.0.1 only**; other machines cannot reach it.
- Each server is **locked to a single database**; it cannot list or query any other
  database on the connection, which narrows what an AI client can reach.
- Requests whose `Host` header is not a loopback name, or whose `Origin` header is
  not a local page, are rejected with **403**. This blocks web pages in a browser
  (including DNS-rebinding attacks) from using the server. AI clients send no
  `Origin` and are not affected.
- There is **no authentication token**: any program running on this computer
  under any user can call the server while it runs. Stop servers you no longer use.
- Servers are **read-only by default** (statement check + rolled-back
  transaction). With **write-access** the AI can do everything the connection's
  login can do, **including changing or deleting data**. A login with only the
  permissions the AI needs (e.g. `db_datareader`) is still the strongest guarantee.
- **Query results are sent to the AI client and its model provider.** Do not
  expose databases with data you may not share, and never paste passwords, API
  keys, tokens or connection strings into the chat.
- Passwords are never shown in the tab or returned by any tool.

## Implementation

| Type | Role |
|---|---|
| `Services/Mcp/IMcpServerHostFactory`, `McpServerHostFactory` | Connects a new `SqlConnectionService` with the profile (validating the login first), finds a port, builds an ASP.NET Core `WebApplication` (Kestrel on `IPAddress.Loopback`, `CreateSlimBuilder`, logging cleared) with the official MCP C# SDK (`AddMcpServer().WithHttpTransport(Stateless = true).WithTools(tools)`, `MapMcp("/mcp")`) and the request guard middleware. `BuildInstructions` describes the locked database, and `settings.MetadataInstructions` (the pasted metadata) is appended to the instructions. Retries the next port when binding fails (`IOException`), up to 10 times. |
| `IMcpServerHost`, `McpServerHost` | A running server: `Port`, `Endpoint`, `StopAsync` (3 s grace, then disposes; safe to call twice). |
| `McpDatabaseTools` | The four tools (`[McpServerToolType]`, `[McpServerTool]`), each timed and reported through `McpServerSettings.ToolCalled`; failures become `CallToolResult { IsError = true }`. `ParseObjectName` splits `schema.name` (bracket-aware; the default schema comes from the active dialect). `execute_query` reads `McpServerSettings.GetAccessMode()` on every call: read-only → `ISqlStatementAnalyzer.FindWriteOperation` (reject) then `ISqlConnectionService.ExecuteReadOnlyBatchAsync`; write-access → `ExecuteBatchAsync`. Every tool is locked to the server's single database via `GetLockViolation` / `LockedDatabase` (a call naming another database is rejected); `list_databases` returns only that database. |
| `SqlStatementAnalyzer.FindWriteOperation` | Strips comments, literals and `[bracket]` / `"quote"` / `` `backtick` `` names, extracts words (skipping `@variables` / `#temp` names), returns the first blocked keyword, or `procedure call` when the first word is not an allowed batch starter. |
| `SqlConnectionService.ExecuteReadOnlyBatchAsync` | Same as `ExecuteBatchAsync` inside a `DbTransaction` that is rolled back after the reader closes (an already-ended transaction is ignored). |
| `Models/McpAccessMode` | `ReadOnly = 1`, `ReadWrite = 2`. |
| `McpResultFormatter` | Compact JSON for results, objects, columns; row and cell limits; value conversion (`ToJsonValue`). |
| `McpPortFinder` | First free loopback port in 9000–9999 (`FindAvailablePort` is pure and unit-tested; `IsLoopbackPortAvailable` probes with an exclusive `TcpListener`). |
| `McpRequestGuard` | `IsAllowedHost` / `IsAllowedOrigin` checks. |
| `Models/McpServerSettings`, `McpToolCall`, `McpServerState` | Start settings (incl. `MetadataInstructions`, appended to the server instructions by the factory), activity log entry, server state (enum from 1). |
| `Models/ConnectionMetadata`, `Services/IMetadataStore`, `MetadataStore` | A pasted JSON metadata document keyed by connection name + database, persisted in `%APPDATA%\LightSQL\metadata.json`. `McpServerTabViewModel.StartAsync` reads it by `profile.Name` + `DefaultDatabase` and passes it as `MetadataInstructions`. |
| `ViewModels/MetadataDialogViewModel`, `Views/MetadataDialog` | The Database Metadata dialog: pick connection + database, paste JSON (validated with `JsonDocument.Parse`), save or clear via `IMetadataStore`. Opened from `MainViewModel.EditDatabaseMetadataCommand` through `IDialogService.ShowMetadataDialog`. |
| `Converters/DatabaseObjectTypeToVisibilityConverter` | Shows the Object Explorer "Start MCP Server" menu item only on a `Database` node. |
| `ViewModels/McpServerTabViewModel` | The tab: state, port, endpoint, client setup text, copy commands (`IClipboardService`), activity log, access mode (`IsReadWrite` with confirmation through `IDialogService`; stored in a `volatile` field read by server threads). Tool calls arrive on server threads and are posted to the UI `SynchronizationContext`. Keeps a clone of the profile. |
| `Views/McpServerTabView` | The tab's content. |
| `Controls/DocumentTabStyleSelector` | Chooses the tab-header style (and context menu) per tab type. |

Package: `ModelContextProtocol.AspNetCore` (brings the `Microsoft.AspNetCore.App`
shared framework into the WPF app).

Tests: `McpPortFinderTests`, `McpRequestGuardTests`, `McpResultFormatterTests`,
`McpDatabaseToolsTests` (incl. a foreign `database` argument rejected and
`list_databases` returning only the locked database), `McpServerTabViewModelTests`
(incl. saved metadata passed as `MetadataInstructions`), `MainViewModelMcpTests`
(incl. "Start MCP" disabled without a selected database and the server node not
starting a server), `MetadataStoreTests`, `MetadataDialogViewModelTests`,
`DatabaseObjectTypeToVisibilityConverterTests`, `SqlStatementAnalyzerTests`
(`FindWriteOperation`), `McpServerHostFactoryTests` (end-to-end: a real server on 9000+
and the SDK's `McpClient` listing tools and calling `execute_query`; a `DELETE` is
refused by default; a foreign database is refused; pasted metadata appears in the server
instructions; a cross-site `Origin` gets 403) and
`ThemedWindowsTests.MainWindow_ShowsMcpServerTab_UnderEveryTheme`.

## Possible next steps

- Optional per-server bearer token (`Authorization` header) for shared machines.
- Remembering servers and restarting them with LightSQL.
