# LightSQL — MCP Server (AI clients)

LightSQL can expose a connection as an **MCP server** (Model Context Protocol), so
AI clients such as **Claude Code**, **Claude Desktop** or **Codex** can explore the
database and run queries from a chat.

## Starting a server

You must be connected first. Then use any of:

- **Database → Start MCP Server** or the **MCP Server** toolbar button. The selected
  query tab's database becomes the server's *default database*.
- Right-click in the **Object Explorer → Start MCP Server**. On a database (or
  anything inside it) that database is the default; on the server node the
  connection's default database is used.

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
| Server | Status, SQL Server instance, default database, how it signs in (Windows / SQL login name; never the password), port, endpoint URL (with **Copy**), transport, start time, number of tool calls, **Access** (the *Allow read-write* check box and the current mode) |
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
| **Read-write** | Any T-SQL: `INSERT`, `UPDATE`, `DELETE`, `MERGE`, DDL, `EXEC`, … with the permissions of the connection's login. |

- Switch with the **Allow read-write (AI can change data)** check box in the tab.
  Turning it **on** asks for confirmation; turning it off does not. The change
  applies to the **next query**, without restarting the server, and is kept when
  the server is stopped and started again from the same tab. A new tab is always
  read-only.
- In read-write mode the tab header shows `[read-write]` and the access text is red.
- **Rejected statements** (keywords found outside strings, comments and
  `[bracketed]` names): `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `TRUNCATE`, `INTO`
  (`SELECT … INTO`), `CREATE`, `ALTER`, `DROP`, `ENABLE`, `DISABLE`, `GRANT`,
  `REVOKE`, `DENY`, `EXEC`/`EXECUTE`, `COMMIT`, `ROLLBACK`, `SAVE`, `TRAN`,
  `TRANSACTION`, `BACKUP`, `RESTORE`, `DBCC`, `BULK`, `CHECKPOINT`, `KILL`,
  `SHUTDOWN`, `RECONFIGURE`, `SETUSER`, `WRITETEXT`, `UPDATETEXT`, `OPENROWSET`,
  `OPENQUERY`, `OPENDATASOURCE`, `SEND`, `RECEIVE`; also a batch that starts with a
  name, which T-SQL runs as a procedure without `EXEC` (`sp_configure 'x', 1`).
  The check errs on the side of blocking: an unbracketed column named like a
  keyword (e.g. `Update`) is rejected — write `[Update]`. Stored procedures and
  table variables / `#temp` inserts need read-write.
- The AI receives an error such as *"Rejected: this MCP server is read-only and
  the batch contains INSERT … The user can switch the server to read-write in
  LightSQL"*; the activity log shows **Blocked (read-only): INSERT**.
- The rollback is the safety net behind the check: whatever a batch changes in a
  read-only call is undone. Not covered by a rollback: sequence values taken by
  `NEXT VALUE FOR` and identity values are not reused (no data is kept).

## Connecting an AI client

The endpoint is `http://127.0.0.1:<port>/mcp` (Streamable HTTP transport).

**Claude Code** (run in a terminal; the tab's **Copy** button gives the exact line):

```bash
claude mcp add --transport http lightsql-9000 http://127.0.0.1:9000/mcp
```

**Codex** — add to `~/.codex/config.toml`:

```toml
[mcp_servers.lightsql-9000]
url = "http://127.0.0.1:9000/mcp"
```

Any other client that supports the Streamable HTTP transport works the same way.
If the server is restarted on another port, update the client's URL.

## Tools

| Tool | Arguments | Returns |
|---|---|---|
| `list_databases` | – | JSON array of online database names |
| `list_tables` | `database?` | `{database, objects:[{schema, name, type: table/view}]}` |
| `describe_table` | `table` (`Orders`, `sales.Orders`, `[sales].[Order Details]`), `schema?`, `database?` | `{database, schema, name, columns:[{name, type, nullable}]}` |
| `execute_query` | `sql`, `database?`, `maxRows?` (default 200, max 5000); read-only unless allowed (see Access mode) | `{success, database, elapsedMs, recordsAffected, messages, resultSets:[{columns:[{name,type}], rows:[[...]], totalRows, truncated}]}` |

- Omitted `database` means the server's default database.
- Values: NULL → `null`, numbers/booleans native, dates ISO 8601, binary `0x...`,
  text longer than 4000 characters is cut with `...`.
- A failed batch (SQL error) returns an MCP *error result* whose text is the same
  JSON, including the SQL Server messages, so the AI can correct its query.
- Each call runs on a **fresh pooled connection**, like the query tabs: `#temp`
  tables, `SET` options and `USE` do not carry over to the next call (the result
  says so when a batch ran `USE`).
- The read-only tools are annotated `readOnly`; `execute_query` is annotated
  `destructive` (annotations are fixed, while the mode can change at runtime), so
  clients that honour annotations ask before running it.
- On connect, the client receives *instructions* naming the server, the default
  database, the read-only default and the rules above.

## Security

- The server listens on **127.0.0.1 only**; other machines cannot reach it.
- Requests whose `Host` header is not a loopback name, or whose `Origin` header is
  not a local page, are rejected with **403**. This blocks web pages in a browser
  (including DNS-rebinding attacks) from using the server. AI clients send no
  `Origin` and are not affected.
- There is **no authentication token**: any program running on this computer
  under any user can call the server while it runs. Stop servers you no longer use.
- Servers are **read-only by default** (statement check + rolled-back
  transaction). In **read-write** mode the AI can do everything the connection's
  login can do, **including changing or deleting data**. A login with only the
  permissions the AI needs (e.g. `db_datareader`) is still the strongest guarantee.
- **Query results are sent to the AI client and its model provider.** Do not
  expose databases with data you may not share, and never paste passwords, API
  keys, tokens or connection strings into the chat.
- Passwords are never shown in the tab or returned by any tool.

## Implementation

| Type | Role |
|---|---|
| `Services/Mcp/IMcpServerHostFactory`, `McpServerHostFactory` | Connects a new `SqlConnectionService` with the profile (validating the login first), finds a port, builds an ASP.NET Core `WebApplication` (Kestrel on `IPAddress.Loopback`, `CreateSlimBuilder`, logging cleared) with the official MCP C# SDK (`AddMcpServer().WithHttpTransport(Stateless = true).WithTools(tools)`, `MapMcp("/mcp")`) and the request guard middleware. Retries the next port when binding fails (`IOException`), up to 10 times. |
| `IMcpServerHost`, `McpServerHost` | A running server: `Port`, `Endpoint`, `StopAsync` (3 s grace, then disposes; safe to call twice). |
| `McpDatabaseTools` | The four tools (`[McpServerToolType]`, `[McpServerTool]`), each timed and reported through `McpServerSettings.ToolCalled`; failures become `CallToolResult { IsError = true }`. `ParseObjectName` splits `schema.name` (bracket-aware). `execute_query` reads `McpServerSettings.GetAccessMode()` on every call: read-only → `ISqlStatementAnalyzer.FindWriteOperation` (reject) then `ISqlConnectionService.ExecuteReadOnlyBatchAsync`; read-write → `ExecuteBatchAsync`. |
| `SqlStatementAnalyzer.FindWriteOperation` | Strips comments, literals and quoted names, extracts words (skipping `@variables` / `#temp` names), returns the first blocked keyword, or `procedure call` when the first word is not an allowed batch starter. |
| `SqlConnectionService.ExecuteReadOnlyBatchAsync` | Same as `ExecuteBatchAsync` inside a `SqlTransaction` that is rolled back after the reader closes (an already-ended transaction is ignored). |
| `Models/McpAccessMode` | `ReadOnly = 1`, `ReadWrite = 2`. |
| `McpResultFormatter` | Compact JSON for results, objects, columns; row and cell limits; value conversion (`ToJsonValue`). |
| `McpPortFinder` | First free loopback port in 9000–9999 (`FindAvailablePort` is pure and unit-tested; `IsLoopbackPortAvailable` probes with an exclusive `TcpListener`). |
| `McpRequestGuard` | `IsAllowedHost` / `IsAllowedOrigin` checks. |
| `Models/McpServerSettings`, `McpToolCall`, `McpServerState` | Start settings, activity log entry, server state (enum from 1). |
| `ViewModels/McpServerTabViewModel` | The tab: state, port, endpoint, client setup text, copy commands (`IClipboardService`), activity log, access mode (`IsReadWrite` with confirmation through `IDialogService`; stored in a `volatile` field read by server threads). Tool calls arrive on server threads and are posted to the UI `SynchronizationContext`. Keeps a clone of the profile. |
| `Views/McpServerTabView` | The tab's content. |
| `Controls/DocumentTabStyleSelector` | Chooses the tab-header style (and context menu) per tab type. |

Package: `ModelContextProtocol.AspNetCore` (brings the `Microsoft.AspNetCore.App`
shared framework into the WPF app).

Tests: `McpPortFinderTests`, `McpRequestGuardTests`, `McpResultFormatterTests`,
`McpDatabaseToolsTests`, `McpServerTabViewModelTests`, `MainViewModelMcpTests`,
`SqlStatementAnalyzerTests` (`FindWriteOperation`),
`McpServerHostFactoryTests` (end-to-end: a real server on 9000+ and the SDK's
`McpClient` listing tools and calling `execute_query`; a `DELETE` is refused by
default; a cross-site `Origin` gets 403) and `ThemedWindowsTests.MainWindow_ShowsMcpServerTab_UnderEveryTheme`.

## Possible next steps

- Optional per-server bearer token (`Authorization` header) for shared machines.
- Remembering servers and restarting them with LightSQL.
