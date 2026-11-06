using System.ComponentModel;
using System.Diagnostics;
using LightSQL.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LightSQL.Services.Mcp
{
    /// <summary>
    /// The tools an MCP server offers to AI clients (Claude, Codex, ...): list the
    /// databases, list a database's tables and views, describe a table's columns
    /// and execute SQL against the connected engine (SQL Server, PostgreSQL or MySQL).
    /// Every call is reported through a callback so the server tab can show what the AI did.
    /// <para>
    /// In read-only mode (the default) execute_query first rejects batches that
    /// could change anything (<see cref="ISqlStatementAnalyzer.FindWriteOperation"/>)
    /// and then runs the rest in a transaction that is always rolled back, so a
    /// change the check missed is still undone. Read-write mode runs batches as is.
    /// </para>
    /// </summary>
    [McpServerToolType]
    public sealed class McpDatabaseTools
    {
        private readonly ISqlConnectionService sqlService;
        private readonly ISqlStatementAnalyzer statementAnalyzer;
        private readonly string? defaultDatabase;
        private readonly Func<McpAccessMode> getAccessMode;
        private readonly Action<McpToolCall>? toolCalled;

        /// <summary>
        /// Creates the tool set for one MCP server.
        /// </summary>
        /// <param name="sqlService">
        /// The server's own, already connected SQL service (not the one the query
        /// tabs use, so disconnecting the main window does not affect the server).
        /// </param>
        /// <param name="statementAnalyzer">Detects statements that could change data (read-only mode).</param>
        /// <param name="defaultDatabase">The database used when a call names none; null for the connection's default.</param>
        /// <param name="getAccessMode">Returns the current access mode; read on every query.</param>
        /// <param name="toolCalled">Called after every tool call; may be null.</param>
        public McpDatabaseTools(
            ISqlConnectionService sqlService,
            ISqlStatementAnalyzer statementAnalyzer,
            string? defaultDatabase,
            Func<McpAccessMode> getAccessMode,
            Action<McpToolCall>? toolCalled)
        {
            this.sqlService = sqlService;
            this.statementAnalyzer = statementAnalyzer;
            this.defaultDatabase = string.IsNullOrWhiteSpace(defaultDatabase) == true ? null : defaultDatabase;
            this.getAccessMode = getAccessMode;
            this.toolCalled = toolCalled;
        }

        /// <summary>
        /// The database used when a tool call does not name one.
        /// </summary>
        public string? DefaultDatabase
        {
            get
            {
                return defaultDatabase ?? sqlService.CurrentDatabase;
            }
        }

        /// <summary>
        /// Splits a table reference into schema and name. Accepts "Orders",
        /// "dbo.Orders" and bracketed forms such as "[dbo].[Order Details]"; an
        /// explicit schema argument wins over one in the name.
        /// </summary>
        /// <param name="table">The table or view name, optionally schema-qualified.</param>
        /// <param name="schema">An explicit schema, or null.</param>
        /// <returns>The schema (default "dbo") and the object name.</returns>
        public static (string Schema, string Name) ParseObjectName(string table, string? schema, string? defaultSchema = "dbo")
        {
            string trimmed = table.Trim();
            string? parsedSchema = null;
            string name = trimmed;

            int separator = FindSchemaSeparator(trimmed);

            if (separator > 0)
            {
                parsedSchema = trimmed.Substring(0, separator);
                name = trimmed.Substring(separator + 1);
            }

            string resolvedSchema = string.IsNullOrWhiteSpace(schema) == false
                ? schema
                : parsedSchema ?? defaultSchema ?? string.Empty;

            return (Unbracket(resolvedSchema), Unbracket(name));
        }

        /// <summary>
        /// Lists the online databases on the server.
        /// </summary>
        /// <param name="cancellationToken">Cancelled when the client abandons the call.</param>
        /// <returns>The database names as a JSON array.</returns>
        [McpServerTool(Name = "list_databases", Title = "List databases", ReadOnly = true, OpenWorld = false)]
        [Description("Lists the databases (schemas, for MySQL) on the connected database server.")]
        public Task<CallToolResult> ListDatabasesAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync("list_databases", string.Empty, null, async () =>
            {
                IReadOnlyList<string> databases = await sqlService.GetDatabasesAsync(cancellationToken);

                return (McpResultFormatter.FormatNames(databases), true, $"{databases.Count} database(s)");
            });
        }

        /// <summary>
        /// Lists the tables and views of a database.
        /// </summary>
        /// <param name="database">The database, or null for the default.</param>
        /// <param name="cancellationToken">Cancelled when the client abandons the call.</param>
        /// <returns>The objects as JSON.</returns>
        [McpServerTool(Name = "list_tables", Title = "List tables and views", ReadOnly = true, OpenWorld = false)]
        [Description("Lists the tables and views (schema, name, type) of a database.")]
        public Task<CallToolResult> ListTablesAsync(
            [Description("Database to inspect. Omit to use the server's default database.")] string? database = null,
            CancellationToken cancellationToken = default)
        {
            string target = ResolveDatabase(database);

            return RunAsync("list_tables", target, target, async () =>
            {
                IReadOnlyList<SchemaObject> tables = await sqlService.GetTablesAsync(target, cancellationToken);
                IReadOnlyList<SchemaObject> views = await sqlService.GetViewsAsync(target, cancellationToken);

                string json = McpResultFormatter.FormatObjects(target, tables, views);

                return (json, true, $"{tables.Count} table(s), {views.Count} view(s)");
            });
        }

        /// <summary>
        /// Lists the columns of a table or view.
        /// </summary>
        /// <param name="table">The table or view name, optionally "schema.name".</param>
        /// <param name="schema">The schema, or null (default "dbo").</param>
        /// <param name="database">The database, or null for the default.</param>
        /// <param name="cancellationToken">Cancelled when the client abandons the call.</param>
        /// <returns>The columns as JSON.</returns>
        [McpServerTool(Name = "describe_table", Title = "Describe a table", ReadOnly = true, OpenWorld = false)]
        [Description("Lists the columns (name, SQL type, nullability) of a table or view.")]
        public Task<CallToolResult> DescribeTableAsync(
            [Description("Table or view name, e.g. 'Orders' or 'sales.Orders'.")] string table,
            [Description("Schema name. Omit to use the schema in 'table', or the engine default (dbo / public; ignored by MySQL).")] string? schema = null,
            [Description("Database that owns the table. Omit to use the server's default database.")] string? database = null,
            CancellationToken cancellationToken = default)
        {
            string target = ResolveDatabase(database);
            (string Schema, string Name) objectName = ParseObjectName(table, schema, sqlService.Dialect?.DefaultSchema);
            string qualified = string.IsNullOrEmpty(objectName.Schema) == true
                ? objectName.Name
                : $"{objectName.Schema}.{objectName.Name}";

            return RunAsync("describe_table", qualified, target, async () =>
            {
                IReadOnlyList<ColumnInfo> columns = await sqlService.GetColumnsAsync(target, objectName.Schema, objectName.Name, cancellationToken);

                if (columns.Count == 0)
                {
                    return ($"Table or view '{qualified}' was not found in database '{target}'.", false, "Not found");
                }

                string json = McpResultFormatter.FormatColumns(target, objectName.Schema, objectName.Name, columns);

                return (json, true, $"{columns.Count} column(s)");
            });
        }

        /// <summary>
        /// Executes a T-SQL batch and returns its result sets and messages.
        /// </summary>
        /// <param name="sql">The T-SQL batch.</param>
        /// <param name="database">The database, or null for the default.</param>
        /// <param name="maxRows">Rows returned per result set.</param>
        /// <param name="cancellationToken">Cancelled when the client abandons the call.</param>
        /// <returns>The result as JSON; an error result when the batch failed.</returns>
        [McpServerTool(Name = "execute_query", Title = "Execute SQL", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Executes a SQL batch on the connected database server (SQL Server, PostgreSQL or MySQL) and "
            + "returns every result set (columns, rows), the messages and the number of rows affected. Use the SQL "
            + "dialect of that engine. The server is read-only unless the user enabled read-write in LightSQL: then "
            + "only queries that read data are accepted (no INSERT/UPDATE/DELETE, DDL, EXEC/CALL or SELECT INTO) "
            + "and the batch is rolled back. "
            + "Each call runs on its own connection: temporary tables, session settings and USE do not carry over to the next call.")]
        public Task<CallToolResult> ExecuteQueryAsync(
            [Description("The SQL batch to execute, in the connected engine's dialect (no GO separators).")] string sql,
            [Description("Database to run in. Omit to use the server's default database.")] string? database = null,
            [Description("Maximum rows returned per result set (default 200, at most 5000).")] int maxRows = McpResultFormatter.DefaultMaxRows,
            CancellationToken cancellationToken = default)
        {
            string target = ResolveDatabase(database);
            int limit = McpResultFormatter.ClampMaxRows(maxRows);

            return RunAsync("execute_query", sql, target, async () =>
            {
                if (string.IsNullOrWhiteSpace(sql) == true)
                {
                    return ("The 'sql' argument is empty.", false, "Empty query");
                }

                bool isReadOnly = getAccessMode() != McpAccessMode.ReadWrite;

                if (isReadOnly == true)
                {
                    string? writeOperation = statementAnalyzer.FindWriteOperation(sql);

                    if (writeOperation is not null)
                    {
                        string message = $"Rejected: this MCP server is read-only and the batch contains {writeOperation}. "
                            + "Only queries that read data are allowed (SELECT / WITH, variables, control flow). "
                            + "The user can switch the server to read-write in LightSQL if changes are intended.";

                        return (message, false, $"Blocked (read-only): {writeOperation}");
                    }
                }

                QueryExecutionResult result = isReadOnly == true
                    ? await sqlService.ExecuteReadOnlyBatchAsync(sql, target, cancellationToken)
                    : await sqlService.ExecuteBatchAsync(sql, target, cancellationToken);
                string json = McpResultFormatter.FormatExecutionResult(result, target, limit);

                string outcome = result.IsSuccess == true
                    ? DescribeSuccess(result)
                    : FirstLine(result.Messages);

                return (json, result.IsSuccess, outcome);
            });
        }

        /// <summary>
        /// Summarises a successful batch for the activity log.
        /// </summary>
        /// <param name="result">The execution result.</param>
        /// <returns>For example "2 result set(s), 15 row(s)" or "3 row(s) affected".</returns>
        private static string DescribeSuccess(QueryExecutionResult result)
        {
            if (result.ResultTables.Count > 0)
            {
                return $"{result.ResultTables.Count} result set(s), {result.TotalRows} row(s)";
            }

            return $"{result.RecordsAffected} row(s) affected";
        }

        /// <summary>
        /// Returns the first non-empty line of a text, used to summarise errors.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The first line, or "Failed" when the text is empty.</returns>
        private static string FirstLine(string text)
        {
            string? line = text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(candidate => candidate.StartsWith("Msg ", StringComparison.Ordinal) == false);

            return line ?? "Failed";
        }

        /// <summary>
        /// Finds the dot that separates schema from name, ignoring dots inside
        /// brackets (e.g. "[my.schema].[t]").
        /// </summary>
        /// <param name="reference">The object reference.</param>
        /// <returns>The index of the separator, or -1 when there is none.</returns>
        private static int FindSchemaSeparator(string reference)
        {
            bool inBrackets = false;

            for (int index = 0; index < reference.Length; index++)
            {
                char current = reference[index];

                if (current == '[')
                {
                    inBrackets = true;
                }
                else if (current == ']')
                {
                    inBrackets = false;
                }
                else if (current == '.' && inBrackets == false)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Removes surrounding brackets from an identifier ("[Order Details]" becomes
        /// "Order Details", "]]" becomes "]").
        /// </summary>
        /// <param name="identifier">The identifier.</param>
        /// <returns>The plain identifier.</returns>
        private static string Unbracket(string identifier)
        {
            string trimmed = identifier.Trim();

            if (trimmed.Length >= 2 && trimmed.StartsWith('[') == true && trimmed.EndsWith(']') == true)
            {
                return trimmed.Substring(1, trimmed.Length - 2).Replace("]]", "]");
            }

            return trimmed;
        }

        /// <summary>
        /// Picks the database for a call: the one named, else the server default.
        /// </summary>
        /// <param name="database">The database named by the call, or null.</param>
        /// <returns>The database to use.</returns>
        private string ResolveDatabase(string? database)
        {
            if (string.IsNullOrWhiteSpace(database) == false)
            {
                return database.Trim();
            }

            // The connection's default, else the engine's administrative database.
            return DefaultDatabase ?? sqlService.AdministrativeDatabase ?? string.Empty;
        }

        /// <summary>
        /// Runs a tool body, times it, turns failures into MCP error results (so the
        /// AI sees the SQL error text) and reports the call to the activity log.
        /// </summary>
        /// <param name="toolName">The tool name.</param>
        /// <param name="detail">The main argument, shown in the log.</param>
        /// <param name="database">The database used, or null.</param>
        /// <param name="body">Returns the result text, whether it succeeded and a short outcome.</param>
        /// <returns>The MCP tool result.</returns>
        private async Task<CallToolResult> RunAsync(string toolName, string detail, string? database, Func<Task<(string Text, bool Success, string Outcome)>> body)
        {
            DateTime started = DateTime.Now;
            Stopwatch stopwatch = Stopwatch.StartNew();
            string text;
            bool success;
            string outcome;

            try
            {
                (text, success, outcome) = await body();
            }
            catch (OperationCanceledException)
            {
                Report(toolName, detail, database, started, stopwatch.Elapsed, false, "Cancelled by the client");

                throw;
            }
            catch (Exception ex)
            {
                text = ex.Message;
                success = false;
                outcome = ex.Message;
            }

            Report(toolName, detail, database, started, stopwatch.Elapsed, success, outcome);

            return new CallToolResult
            {
                Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
                IsError = success == false
            };
        }

        /// <summary>
        /// Sends one entry to the activity log callback.
        /// </summary>
        private void Report(string toolName, string detail, string? database, DateTime started, TimeSpan elapsed, bool success, string outcome)
        {
            toolCalled?.Invoke(new McpToolCall
            {
                Timestamp = started,
                ToolName = toolName,
                Detail = detail,
                Database = database,
                IsSuccess = success,
                Outcome = outcome,
                Elapsed = elapsed
            });
        }
    }
}
