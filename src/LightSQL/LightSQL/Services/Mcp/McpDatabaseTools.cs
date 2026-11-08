using System.ComponentModel;
using LightSQL.Models;
using LightSQL.Services.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LightSQL.Services.Mcp
{
    /// <summary>
    /// The MCP adapter over <see cref="DatabaseToolExecutor"/>: exposes the database tools
    /// (list databases, list a database's tables and views, describe a table's columns and
    /// execute SQL) to AI clients (Claude, Codex, ...) through the MCP C# SDK. All logic,
    /// safety (single-database lock, read-only/write-access) and output live in the executor,
    /// which the in-app chat shares; this type only maps to and from <see cref="CallToolResult"/>.
    /// </summary>
    [McpServerToolType]
    public sealed class McpDatabaseTools
    {
        private readonly DatabaseToolExecutor executor;

        /// <summary>
        /// Creates the tool set for one MCP server.
        /// </summary>
        /// <param name="sqlService">
        /// The server's own, already connected SQL service (not the one the query tabs use, so
        /// disconnecting the main window does not affect the server).
        /// </param>
        /// <param name="statementAnalyzer">Detects statements that could change data (read-only mode).</param>
        /// <param name="defaultDatabase">The single database to serve; null for the connection's default.</param>
        /// <param name="getAccessMode">Returns the current access mode; read on every query.</param>
        /// <param name="toolCalled">Called after every tool call; may be null.</param>
        public McpDatabaseTools(
            ISqlConnectionService sqlService,
            ISqlStatementAnalyzer statementAnalyzer,
            string? defaultDatabase,
            Func<McpAccessMode> getAccessMode,
            Action<McpToolCall>? toolCalled)
        {
            executor = new DatabaseToolExecutor(sqlService, statementAnalyzer, defaultDatabase, getAccessMode, toolCalled);
        }

        /// <summary>
        /// The database used when a tool call does not name one.
        /// </summary>
        public string? DefaultDatabase
        {
            get
            {
                return executor.DefaultDatabase;
            }
        }

        /// <summary>
        /// Splits a table reference into schema and name (see
        /// <see cref="DatabaseToolExecutor.ParseObjectName"/>).
        /// </summary>
        /// <param name="table">The table or view name, optionally schema-qualified.</param>
        /// <param name="schema">An explicit schema, or null.</param>
        /// <param name="defaultSchema">The engine's default schema, or null.</param>
        /// <returns>The schema (default "dbo") and the object name.</returns>
        public static (string Schema, string Name) ParseObjectName(string table, string? schema, string? defaultSchema = "dbo")
        {
            return DatabaseToolExecutor.ParseObjectName(table, schema, defaultSchema);
        }

        /// <summary>
        /// Lists the database this server is locked to.
        /// </summary>
        /// <param name="cancellationToken">Cancelled when the client abandons the call.</param>
        /// <returns>The database name as a JSON array.</returns>
        [McpServerTool(Name = "list_databases", Title = "List databases", ReadOnly = true, OpenWorld = false)]
        [Description("Lists the database this server is locked to (a single-item list).")]
        public async Task<CallToolResult> ListDatabasesAsync(CancellationToken cancellationToken = default)
        {
            return ToResult(await executor.ListDatabasesAsync(cancellationToken));
        }

        /// <summary>
        /// Lists the tables and views of the locked database.
        /// </summary>
        /// <param name="database">The database named by the call (rejected unless it is the locked one).</param>
        /// <param name="cancellationToken">Cancelled when the client abandons the call.</param>
        /// <returns>The objects as JSON.</returns>
        [McpServerTool(Name = "list_tables", Title = "List tables and views", ReadOnly = true, OpenWorld = false)]
        [Description("Lists the tables and views (schema, name, type) of a database.")]
        public async Task<CallToolResult> ListTablesAsync(
            [Description("Omit this. The server is locked to a single database; naming another one is rejected.")] string? database = null,
            CancellationToken cancellationToken = default)
        {
            return ToResult(await executor.ListTablesAsync(database, cancellationToken));
        }

        /// <summary>
        /// Lists the columns of a table or view in the locked database.
        /// </summary>
        /// <param name="table">The table or view name, optionally "schema.name".</param>
        /// <param name="schema">The schema, or null (default "dbo").</param>
        /// <param name="database">The database named by the call (rejected unless it is the locked one).</param>
        /// <param name="cancellationToken">Cancelled when the client abandons the call.</param>
        /// <returns>The columns as JSON.</returns>
        [McpServerTool(Name = "describe_table", Title = "Describe a table", ReadOnly = true, OpenWorld = false)]
        [Description("Lists the columns (name, SQL type, nullability) of a table or view.")]
        public async Task<CallToolResult> DescribeTableAsync(
            [Description("Table or view name, e.g. 'Orders' or 'sales.Orders'.")] string table,
            [Description("Schema name. Omit to use the schema in 'table', or the engine default (dbo / public; ignored by MySQL).")] string? schema = null,
            [Description("Omit this. The server is locked to a single database; naming another one is rejected.")] string? database = null,
            CancellationToken cancellationToken = default)
        {
            return ToResult(await executor.DescribeTableAsync(table, schema, database, cancellationToken));
        }

        /// <summary>
        /// Executes a SQL batch and returns its result sets and messages.
        /// </summary>
        /// <param name="sql">The SQL batch.</param>
        /// <param name="database">The database named by the call (rejected unless it is the locked one).</param>
        /// <param name="maxRows">Rows returned per result set.</param>
        /// <param name="cancellationToken">Cancelled when the client abandons the call.</param>
        /// <returns>The result as JSON; an error result when the batch failed.</returns>
        [McpServerTool(Name = "execute_query", Title = "Execute SQL", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Executes a SQL batch on the connected database server (SQL Server, PostgreSQL or MySQL) and "
            + "returns every result set (columns, rows), the messages and the number of rows affected. Use the SQL "
            + "dialect of that engine. The server is read-only unless the user granted write-access in LightSQL: then "
            + "only queries that read data are accepted (no INSERT/UPDATE/DELETE, DDL, EXEC/CALL or SELECT INTO) "
            + "and the batch is rolled back. "
            + "Each call runs on its own connection: temporary tables, session settings and USE do not carry over to the next call.")]
        public async Task<CallToolResult> ExecuteQueryAsync(
            [Description("The SQL batch to execute, in the connected engine's dialect (no GO separators).")] string sql,
            [Description("Omit this. The server is locked to a single database; naming another one is rejected.")] string? database = null,
            [Description("Maximum rows returned per result set (default 200, at most 5000).")] int maxRows = McpResultFormatter.DefaultMaxRows,
            CancellationToken cancellationToken = default)
        {
            return ToResult(await executor.ExecuteQueryAsync(sql, database, maxRows, cancellationToken));
        }

        /// <summary>
        /// Maps an executor outcome to an MCP tool result.
        /// </summary>
        /// <param name="outcome">The executor outcome.</param>
        /// <returns>The MCP tool result.</returns>
        private static CallToolResult ToResult(ToolCallOutcome outcome)
        {
            return new CallToolResult
            {
                Content = new List<ContentBlock> { new TextContentBlock { Text = outcome.Text } },
                IsError = outcome.IsError
            };
        }
    }
}
