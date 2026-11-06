# LightSQL — Database Providers (multi-engine support)

LightSQL works with **Microsoft SQL Server**, **PostgreSQL** and **MySQL / MariaDB**.
All engine-specific behaviour lives behind two abstractions so the rest of the app
(view models, MCP server, stores) stays engine-agnostic, and a new engine can be
added without touching that code.

## The two abstractions

Both live in `Services/Database/` (namespace `LightSQL.Services.Database`).

### `ISqlDialect` — pure SQL text

Side-effect-free generation of engine-specific SQL: identifier quoting, "top N"
queries and CREATE / DROP statements. No I/O, so it is trivially unit-testable and
shared by both the provider and the UI.

| Member | SQL Server | PostgreSQL | MySQL |
|---|---|---|---|
| `QuoteIdentifier` | `[x]` | `"x"` | `` `x` `` |
| `DefaultSchema` | `dbo` | `public` | `null` (no schema layer) |
| `BuildSelectTop` | `SELECT TOP n *` | `… LIMIT n` | `… LIMIT n` |
| `QualifyName` | `[s].[n]` | `"s"."n"` | `` `n` `` (schema ignored) |

`SqlDialectBase` supplies everything from the quote characters and the "top N"
shape, so each concrete dialect is a few lines (`SqlServerDialect`,
`PostgreSqlDialect`, `MySqlDialect`).

### `IDatabaseProvider` — connections, metadata, errors

Everything that needs a driver: creating `DbConnection`s, building connection
strings, reading metadata (databases / tables / views / columns), surfacing
messages (PRINT / notices / info), formatting provider errors, and raw-string
password handling. It also exposes its `Dialect`, whether it
`SupportsWindowsAuthentication`, its `DefaultPort`, and the `AdministrativeDatabase`
used for server-level work.

`DatabaseProviderBase` holds the generic reader loops (over `DbCommand` /
`DbDataReader`) and the server description, so each concrete provider
(`SqlServerDatabaseProvider`, `PostgreSqlDatabaseProvider`, `MySqlDatabaseProvider`)
supplies only its own SQL, mapping and connection-string building.

| Concern | SQL Server | PostgreSQL | MySQL |
|---|---|---|---|
| Driver | `Microsoft.Data.SqlClient` | `Npgsql` | `MySqlConnector` |
| List databases | `sys.databases` | `pg_database` | `information_schema.schemata` |
| Tables / views / columns | `sys.*` | `information_schema` | `information_schema` |
| Administrative database | `master` | `postgres` | *(any)* |
| Current database | `DB_NAME()` | `current_database()` | `DATABASE()` |
| Messages | `InfoMessage` | `Notice` | `InfoMessage` |
| Default port | 1433 | 5432 | 3306 |

### `IDatabaseProviderRegistry`

`DatabaseProviderRegistry` maps a `DatabaseEngine` to its provider and exposes all
providers (for the connection dialog's engine picker). It is a DI singleton and is
also created directly by the MCP server factory, which needs its own connection state.

## How the app uses them

- `ConnectionProfile.Engine` (a `DatabaseEngine`, default `SqlServer`) selects the
  provider. Profiles saved before multi-engine support have no stored engine and so
  load as SQL Server.
- `SqlConnectionService` owns the connection lifetime over the ADO.NET base types
  (`DbConnection` / `DbCommand` / `DbDataReader` / `DbTransaction`) and delegates
  every engine-specific decision to the active provider. It exposes `Dialect`,
  `AdministrativeDatabase` and `Engine` for consumers.
- `ObjectExplorerViewModel` builds "Select Top", CREATE and DROP SQL through
  `ISqlConnectionService.Dialect` — never hard-coded T-SQL.
- `McpDatabaseTools` / `McpServerHostFactory` take the default schema, administrative
  database and engine name from the active provider / dialect.
- `ConnectionStore` uses the profile's provider to strip / re-inject passwords in raw
  connection strings.

## Adding a new engine

1. Add a value to `Models/DatabaseEngine.cs` (start from the next number).
2. Implement `ISqlDialect` (extend `SqlDialectBase`) — quote characters, default
   schema, "top N" shape.
3. Implement `IDatabaseProvider` (extend `DatabaseProviderBase`) — connection
   creation, connection-string building, the four metadata queries, the current-database
   query, the message handler, error formatting and raw-string password handling.
   Add the driver's NuGet package to `LightSQL.csproj`.
4. Register the provider in `DatabaseProviderRegistry`'s default constructor.
5. Add unit tests mirroring the existing dialect and provider tests.

No other code changes: the dialog's engine picker, the connection service, the
Object Explorer and the MCP server all pick it up through the registry.

## Encryption (SSL/TLS)

The dialog's **Encrypt** / **Trust server certificate** checkboxes map onto each
engine's SSL mode:

| Encrypt | Trust cert | SQL Server | PostgreSQL | MySQL |
|---|---|---|---|---|
| off | – | `Encrypt=false` | `SslMode=Disable` | `SslMode=None` |
| on | on | `TrustServerCertificate=true` | `SslMode=Prefer` | `SslMode=Preferred` |
| on | off | validate certificate | `SslMode=VerifyFull` | `SslMode=VerifyFull` |

## Tests

`SqlServerDialectTests`, `PostgreSqlDialectTests`, `MySqlDialectTests`,
`DatabaseProviderRegistryTests`, and the connection-string tests
`SqlServerDatabaseProviderTests`, `PostgreSqlDatabaseProviderTests`,
`MySqlDatabaseProviderTests`. The connection-per-operation metadata paths run
against a live server (not unit-tested).
