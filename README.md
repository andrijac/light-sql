# LightSQL

A lightweight, SSMS-style **SQL client for Windows**. LightSQL is a WPF desktop
application (targeting **.NET 10**, `net10.0-windows`) for connecting to a database,
browsing its objects, and writing and running queries — with multiple query tabs,
an object explorer, theming, and a built-in MCP server that lets AI clients query
the database.

It supports **Microsoft SQL Server**, **PostgreSQL** and **MySQL / MariaDB** behind
a common engine abstraction, so the same UI works across all three.

---

## Features

- **Multi-tab query editor (SSMS-style).** Several queries open at once, each tab
  with its own editor (text, undo history, caret, selection), its own database, its
  own Results/Messages, and its own execution and cancellation. Tabs can run
  concurrently.
- **SQL editor** with syntax highlighting (tuned for T-SQL; keywords are largely
  shared across engines), line numbers, and standard editing (undo/redo, cut/copy/
  paste, select-all). **F5** runs the selected text, or the whole editor if nothing
  is selected.
- **Results & Messages.** One grid per result set, grids that stretch to fill the
  pane and follow the window size; informational output, row counts and errors in a
  separate Messages tab. The footer shows status, rows returned/affected, elapsed
  time, and caret line/column.
- **Object Explorer.** A lazy tree — server → Databases → *database* → Tables /
  Views → *object* → columns — each level loaded on expand, with per-type icons.
  Right-click actions: New Query, Select Top 1000, New Database, Drop (with
  confirmation), Start MCP Server (database nodes only), Refresh.
- **Active database per tab.** The toolbar **Database** dropdown selects the database
  for the selected tab; each tab remembers its own. `USE` inside a query switches
  that tab's database, like SSMS.
- **UPDATE / DELETE safety guard.** Before executing, LightSQL warns on an `UPDATE`
  or `DELETE` with no `WHERE` clause (ignoring matches inside strings, comments,
  bracketed identifiers and subqueries). Can be turned off in Options.
- **File handling.** Queries are ordinary `.sql` files — New / Open / Save / Save As,
  drag-and-drop onto the window, command-line arguments (so LightSQL works as
  "Open with" for `.sql`), and encoding round-tripping (a file is saved back in the
  encoding it was opened with; new files are UTF-8 with BOM, like SSMS).
- **Theming.** Five themes — Auto (follow Windows), Light, Dark, Classic (Windows
  Forms gray), and Gradient — chosen in **Tools → Options**, with live preview.
- **MCP server for AI clients.** Expose a single database as a Model Context Protocol
  server — **locked to that database** — so AI clients (Claude Code, Claude Desktop,
  Codex, …) can list its tables and columns and run SQL from a chat. **Read-only by
  default**; opt-in write-access per server. You can also paste schema/semantic metadata
  (JSON) that is sent to the AI as extra instructions. See
  [MCP server](#mcp-server-for-ai-clients) below.

For full details see the [`docs/`](docs/) folder — in particular
[features.md](docs/features.md), [architecture.md](docs/architecture.md) and
[mcp-server.md](docs/mcp-server.md).

---

## Supported databases

| Engine | Driver (NuGet) | Default port |
|---|---|---|
| **Microsoft SQL Server** | `Microsoft.Data.SqlClient` | 1433 |
| **PostgreSQL** | `Npgsql` | 5432 |
| **MySQL / MariaDB** | `MySqlConnector` | 3306 |

The engine is chosen in the connection dialog's **Database type** picker and stored
with each saved connection. All engine-specific behaviour (connections, metadata,
SQL dialect, SSL mode) lives behind a provider abstraction, so a new engine can be
added without touching the rest of the app. See
[docs/database-providers.md](docs/database-providers.md).

- **Authentication:** Windows (Integrated) vs SQL Server login is offered for SQL
  Server only; PostgreSQL and MySQL always use username/password.
- **Encryption:** the **Encrypt** / **Trust server certificate** checkboxes map onto
  each engine's SSL mode.

---

## Running the app

From the repository root:

```bash
run.cmd
```

`run.cmd` builds the app (Debug) and starts it. It needs the **.NET 10 SDK**.
`.sql` files passed as arguments open in query tabs (`run.cmd scripts\report.sql`).
If LightSQL is already running, the build cannot replace the locked `.exe`, so the
script reports that and stops.

---

## Where settings and credentials are stored

All application configuration lives under the current user's profile, in
`%APPDATA%\LightSQL`:

| Data | Location |
|---|---|
| Application options (theme, editor options, UPDATE/DELETE guard) | `%APPDATA%\LightSQL\options.json` |
| Saved connections | `%APPDATA%\LightSQL\connections.json` |
| Query scripts | Wherever you save them — ordinary `.sql` files |

(These paths come from `AppPaths`; options and connections are stored as JSON, with
enum values such as the theme stored by name.)

### Credential handling

LightSQL is designed so a **plaintext password is never written to disk**:

- **Windows / Integrated authentication** (SQL Server) stores no password at all.
- **Username / password** is saved only when **Save password** is ticked. The value
  is encrypted with the **Windows Data Protection API (DPAPI)**, scoped to the
  current Windows user (`DataProtectionScope.CurrentUser`) plus an
  application-specific entropy value. Only the Base64 ciphertext
  (`connections.json`'s `protectedPassword` field) is written — never the plaintext.
- A DPAPI-encrypted value can only be decrypted by the **same Windows user on the
  same machine**. Copying `connections.json` to another user or machine makes the
  saved passwords unreadable (treated as absent).
- For a **raw connection string**, any embedded password is **stripped out** before
  the string is saved; if Save password is ticked, it is moved into the encrypted
  field instead.
- In the UI the password lives only in the `PasswordBox` and is never placed into a
  bindable property.

> **Treat passwords, connection strings and tokens as sensitive.** Query `.sql`
> files are plain text — don't put credentials in them, and don't commit
> `connections.json` to source control. Full detail:
> [docs/connections-and-security.md](docs/connections-and-security.md).

---

## MCP server for AI clients

The **Start MCP** toolbar button (next to the database dropdown), **Database → Start
MCP Server**, or a **database node's** Object Explorer context menu exposes the selected
database as an **MCP** (Model Context Protocol) server in its own tab — **locked to that
one database** — so AI clients can explore it and run SQL from a
chat.

- Listens on **127.0.0.1 only**, on the first free port from **9000** up; several
  servers can run at once. Endpoint: `http://127.0.0.1:<port>/mcp` (Streamable HTTP).
- **Read-only by default** — data-changing statements are rejected and every batch
  runs in a rolled-back transaction. A per-server **Allow write-access** checkbox
  (with confirmation) lets the AI change data.
- Requests with a non-loopback `Host`/`Origin` are rejected (403), blocking browser
  pages. There is **no auth token**, so any program on the machine can call a running
  server — stop servers you no longer use.
- **Query results are sent to the AI client and its model provider.** Use a login
  with the least permissions needed, and don't expose data you can't share.

Tools: `list_databases`, `list_tables`, `describe_table`, `execute_query`.
Full detail: [docs/mcp-server.md](docs/mcp-server.md).

---

## External dependencies

Beyond the **.NET 10** runtime/SDK, LightSQL uses these NuGet packages:

| Package | Role |
|---|---|
| `AvalonEdit` | SQL editor: syntax highlighting, line numbers, caret tracking |
| `Microsoft.Data.SqlClient` | Microsoft SQL Server connectivity |
| `Npgsql` | PostgreSQL connectivity |
| `MySqlConnector` | MySQL / MariaDB connectivity |
| `CommunityToolkit.Mvvm` | MVVM helpers (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`) |
| `Microsoft.Extensions.DependencyInjection` | Service / view-model wiring |
| `ModelContextProtocol.AspNetCore` | MCP server (official C# SDK, hosted in Kestrel) |

**Windows DPAPI** (`System.Security.Cryptography.ProtectedData`), used to encrypt
saved passwords, is provided by the `net10.0-windows` framework and needs no package
reference. Because LightSQL is a WPF app using DPAPI and the Windows system theme,
it runs on **Windows only**.

---

## Project layout

```
run.cmd                 Builds (Debug) and starts the app
docs/                   Project documentation (features, architecture, security, …)
src/LightSQL/
├─ LightSQL.slnx        Solution (LightSQL + LightSQL.Tests)
├─ LightSQL/            The WPF application (Models, Services, ViewModels, Views, …)
└─ LightSQL.Tests/      xUnit unit tests
```

The app follows **MVVM** with constructor dependency injection; all I/O and SQL
access sits behind interfaces. Architecture overview:
[docs/architecture.md](docs/architecture.md).

---

## Documentation

| Topic | Document |
|---|---|
| Features & usage | [docs/features.md](docs/features.md) |
| Architecture | [docs/architecture.md](docs/architecture.md) |
| Connections & security | [docs/connections-and-security.md](docs/connections-and-security.md) |
| Database providers (multi-engine) | [docs/database-providers.md](docs/database-providers.md) |
| MCP server | [docs/mcp-server.md](docs/mcp-server.md) |
| Themes | [docs/themes.md](docs/themes.md) |
| Coding standards | [docs/coding-standards.md](docs/coding-standards.md) |
