using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using LightSQL.Models;
using LightSQL.Services.Mcp;

namespace LightSQL.Services.Tools
{
    /// <summary>
    /// The transport-neutral core of the database tools (list databases, list a database's
    /// tables and views, describe a table's columns, execute SQL). Both the MCP server
    /// (<c>McpDatabaseTools</c>) and the in-app chat agent run their tool calls through this,
    /// so the logic, safety and output are identical.
    /// <para>
    /// The executor is locked to a single database (its default). A tool call that names
    /// another database is rejected, and <c>list_databases</c> returns only that database.
    /// </para>
    /// <para>
    /// In read-only mode (the default) <c>execute_query</c> first rejects batches that could
    /// change anything (<see cref="ISqlStatementAnalyzer.FindWriteOperation"/>) and then runs
    /// the rest in a transaction that is always rolled back. Write-access runs batches as is.
    /// </para>
    /// </summary>
    public sealed class DatabaseToolExecutor
    {
        private readonly ISqlConnectionService sqlService;
        private readonly ISqlStatementAnalyzer statementAnalyzer;
        private readonly string? defaultDatabase;
        private readonly Func<McpAccessMode> getAccessMode;
        private readonly Action<McpToolCall>? toolCalled;

        /// <summary>
        /// Creates the executor for one connection and database.
        /// </summary>
        /// <param name="sqlService">The already-connected SQL service the tools run against.</param>
        /// <param name="statementAnalyzer">Detects statements that could change data (read-only mode).</param>
        /// <param name="defaultDatabase">The single database to operate on; null for the connection's default.</param>
        /// <param name="getAccessMode">Returns the current access mode; read on every query.</param>
        /// <param name="toolCalled">Called after every tool call; may be null.</param>
        public DatabaseToolExecutor(
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
        /// The single database this executor is locked to. Every tool operates only on this
        /// database; a tool call naming any other database is rejected.
        /// </summary>
        private string LockedDatabase
        {
            get
            {
                return DefaultDatabase ?? sqlService.AdministrativeDatabase ?? string.Empty;
            }
        }

        /// <summary>
        /// Splits a table reference into schema and name. Accepts "Orders", "dbo.Orders" and
        /// bracketed forms such as "[dbo].[Order Details]"; an explicit schema argument wins
        /// over one in the name.
        /// </summary>
        /// <param name="table">The table or view name, optionally schema-qualified.</param>
        /// <param name="schema">An explicit schema, or null.</param>
        /// <param name="defaultSchema">The engine's default schema, or null.</param>
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
        /// Runs a tool by name with a parsed-JSON argument object. Used by the chat agent,
        /// which gets the tool name and arguments from the LLM.
        /// </summary>
        /// <param name="toolName">The tool name (see <see cref="DatabaseToolCatalog"/>).</param>
        /// <param name="arguments">The arguments object, or null.</param>
        /// <param name="cancellationToken">Cancelled when the call is abandoned.</param>
        /// <returns>The outcome.</returns>
        public Task<ToolCallOutcome> ExecuteAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken)
        {
            switch (toolName)
            {
                case "list_databases":
                {
                    return ListDatabasesAsync(cancellationToken);
                }

                case "list_tables":
                {
                    return ListTablesAsync(GetString(arguments, "database"), cancellationToken);
                }

                case "describe_table":
                {
                    return DescribeTableAsync(GetString(arguments, "table") ?? string.Empty, GetString(arguments, "schema"), GetString(arguments, "database"), cancellationToken);
                }

                case "execute_query":
                {
                    return ExecuteQueryAsync(GetString(arguments, "sql") ?? string.Empty, GetString(arguments, "database"), GetInt(arguments, "maxRows", McpResultFormatter.DefaultMaxRows), cancellationToken);
                }

                default:
                {
                    return Task.FromResult(new ToolCallOutcome { Text = $"Unknown tool '{toolName}'.", IsError = true, Outcome = "Unknown tool" });
                }
            }
        }

        /// <summary>
        /// Lists the database this executor is locked to (a single-item list).
        /// </summary>
        /// <param name="cancellationToken">Cancelled when the call is abandoned.</param>
        /// <returns>The outcome with the database name as a JSON array.</returns>
        public Task<ToolCallOutcome> ListDatabasesAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync("list_databases", string.Empty, null, () =>
            {
                string locked = LockedDatabase;

                IReadOnlyList<string> databases = string.IsNullOrEmpty(locked) == true
                    ? Array.Empty<string>()
                    : new[] { locked };

                return Task.FromResult((McpResultFormatter.FormatNames(databases), true, $"{databases.Count} database(s)"));
            });
        }

        /// <summary>
        /// Lists the tables and views of the locked database.
        /// </summary>
        /// <param name="database">The database named by the call, or null for the locked one.</param>
        /// <param name="cancellationToken">Cancelled when the call is abandoned.</param>
        /// <returns>The outcome with the objects as JSON.</returns>
        public Task<ToolCallOutcome> ListTablesAsync(string? database, CancellationToken cancellationToken = default)
        {
            string target = LockedDatabase;

            return RunAsync("list_tables", target, target, async () =>
            {
                string? violation = GetLockViolation(database);

                if (violation is not null)
                {
                    return (violation, false, "Blocked (locked database)");
                }

                IReadOnlyList<SchemaObject> tables = await sqlService.GetTablesAsync(target, cancellationToken);
                IReadOnlyList<SchemaObject> views = await sqlService.GetViewsAsync(target, cancellationToken);

                string json = McpResultFormatter.FormatObjects(target, tables, views);

                return (json, true, $"{tables.Count} table(s), {views.Count} view(s)");
            });
        }

        /// <summary>
        /// Lists the columns of a table or view in the locked database.
        /// </summary>
        /// <param name="table">The table or view name, optionally "schema.name".</param>
        /// <param name="schema">The schema, or null (default "dbo").</param>
        /// <param name="database">The database named by the call, or null for the locked one.</param>
        /// <param name="cancellationToken">Cancelled when the call is abandoned.</param>
        /// <returns>The outcome with the columns as JSON.</returns>
        public Task<ToolCallOutcome> DescribeTableAsync(string table, string? schema, string? database, CancellationToken cancellationToken = default)
        {
            string target = LockedDatabase;
            (string Schema, string Name) objectName = ParseObjectName(table, schema, sqlService.Dialect?.DefaultSchema);
            string qualified = string.IsNullOrEmpty(objectName.Schema) == true
                ? objectName.Name
                : $"{objectName.Schema}.{objectName.Name}";

            return RunAsync("describe_table", qualified, target, async () =>
            {
                string? violation = GetLockViolation(database);

                if (violation is not null)
                {
                    return (violation, false, "Blocked (locked database)");
                }

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
        /// Executes a SQL batch and returns its result sets and messages.
        /// </summary>
        /// <param name="sql">The SQL batch.</param>
        /// <param name="database">The database named by the call, or null for the locked one.</param>
        /// <param name="maxRows">Rows returned per result set.</param>
        /// <param name="cancellationToken">Cancelled when the call is abandoned.</param>
        /// <returns>The outcome with the result as JSON; an error outcome when the batch failed.</returns>
        public Task<ToolCallOutcome> ExecuteQueryAsync(string sql, string? database, int maxRows = McpResultFormatter.DefaultMaxRows, CancellationToken cancellationToken = default)
        {
            string target = LockedDatabase;
            int limit = McpResultFormatter.ClampMaxRows(maxRows);

            return RunAsync("execute_query", sql, target, async () =>
            {
                string? violation = GetLockViolation(database);

                if (violation is not null)
                {
                    return (violation, false, "Blocked (locked database)");
                }

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
                        string message = $"Rejected: this server is read-only and the batch contains {writeOperation}. "
                            + "Only queries that read data are allowed (SELECT / WITH, variables, control flow). "
                            + "The user can grant write-access in LightSQL if changes are intended.";

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
        /// Finds the dot that separates schema from name, ignoring dots inside brackets
        /// (e.g. "[my.schema].[t]").
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
        /// Reads a string argument from a JSON arguments object, or null when absent.
        /// </summary>
        /// <param name="arguments">The arguments object, or null.</param>
        /// <param name="name">The argument name.</param>
        /// <returns>The value, or null.</returns>
        private static string? GetString(JsonObject? arguments, string name)
        {
            if (arguments is null || arguments.TryGetPropertyValue(name, out JsonNode? node) == false || node is null)
            {
                return null;
            }

            return node.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : node.ToString();
        }

        /// <summary>
        /// Reads an integer argument from a JSON arguments object, or a fallback when absent
        /// or not a number.
        /// </summary>
        /// <param name="arguments">The arguments object, or null.</param>
        /// <param name="name">The argument name.</param>
        /// <param name="fallback">The value used when the argument is absent or invalid.</param>
        /// <returns>The value, or the fallback.</returns>
        private static int GetInt(JsonObject? arguments, string name, int fallback)
        {
            if (arguments is null || arguments.TryGetPropertyValue(name, out JsonNode? node) == false || node is null)
            {
                return fallback;
            }

            try
            {
                return node.GetValue<int>();
            }
            catch (Exception)
            {
                return int.TryParse(node.ToString(), out int value) == true ? value : fallback;
            }
        }

        /// <summary>
        /// Checks a tool call's requested database against the one this executor is locked to.
        /// </summary>
        /// <param name="requested">The database named by the call, or null/empty for the locked one.</param>
        /// <returns>An error message when another database was requested, or null when allowed.</returns>
        private string? GetLockViolation(string? requested)
        {
            if (string.IsNullOrWhiteSpace(requested) == true)
            {
                return null;
            }

            string locked = LockedDatabase;

            if (string.Equals(requested.Trim(), locked, StringComparison.OrdinalIgnoreCase) == true)
            {
                return null;
            }

            return $"Rejected: this server is locked to database '{locked}' and cannot access '{requested.Trim()}'. "
                + "Omit the 'database' argument, or set it to the server's database.";
        }

        /// <summary>
        /// Runs a tool body, times it, turns failures into error outcomes (so the caller sees
        /// the error text) and reports the call to the activity log.
        /// </summary>
        /// <param name="toolName">The tool name.</param>
        /// <param name="detail">The main argument, shown in the log.</param>
        /// <param name="database">The database used, or null.</param>
        /// <param name="body">Returns the result text, whether it succeeded and a short outcome.</param>
        /// <returns>The outcome.</returns>
        private async Task<ToolCallOutcome> RunAsync(string toolName, string detail, string? database, Func<Task<(string Text, bool Success, string Outcome)>> body)
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

            return new ToolCallOutcome
            {
                Text = text,
                IsError = success == false,
                Outcome = outcome
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
