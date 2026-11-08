namespace LightSQL.Services.Tools
{
    /// <summary>
    /// The database tools the app offers, defined once so the MCP server and the in-app chat
    /// expose the same set. The names and parameters match the MCP tools in
    /// <c>McpDatabaseTools</c> (asserted by a test), and <see cref="DatabaseToolExecutor"/>
    /// runs them.
    /// </summary>
    public static class DatabaseToolCatalog
    {
        /// <summary>
        /// The tool definitions, in a stable order.
        /// </summary>
        public static IReadOnlyList<ToolDefinition> Tools { get; } = new ToolDefinition[]
        {
            new()
            {
                Name = "list_databases",
                Description = "Lists the database this assistant is locked to (a single-item list).",
                Parameters = Array.Empty<ToolParameter>()
            },
            new()
            {
                Name = "list_tables",
                Description = "Lists the tables and views (schema, name, type) of the database.",
                Parameters = new ToolParameter[]
                {
                    new()
                    {
                        Name = "database",
                        Type = "string",
                        Description = "Omit this. The assistant is bound to a single database; naming another one is rejected.",
                        Required = false
                    }
                }
            },
            new()
            {
                Name = "describe_table",
                Description = "Lists the columns (name, SQL type, nullability) of a table or view.",
                Parameters = new ToolParameter[]
                {
                    new()
                    {
                        Name = "table",
                        Type = "string",
                        Description = "Table or view name, e.g. 'Orders' or 'sales.Orders'.",
                        Required = true
                    },
                    new()
                    {
                        Name = "schema",
                        Type = "string",
                        Description = "Schema name. Omit to use the schema in 'table', or the engine default.",
                        Required = false
                    },
                    new()
                    {
                        Name = "database",
                        Type = "string",
                        Description = "Omit this. The assistant is bound to a single database; naming another one is rejected.",
                        Required = false
                    }
                }
            },
            new()
            {
                Name = "execute_query",
                Description = "Executes a SQL batch in the connected engine's dialect and returns every result set "
                    + "(columns, rows), the messages and the number of rows affected. Read-only unless the user "
                    + "granted write-access, in which case data-changing statements are rejected and the batch is "
                    + "rolled back. Each call runs on its own connection.",
                Parameters = new ToolParameter[]
                {
                    new()
                    {
                        Name = "sql",
                        Type = "string",
                        Description = "The SQL batch to execute, in the connected engine's dialect (no GO separators).",
                        Required = true
                    },
                    new()
                    {
                        Name = "database",
                        Type = "string",
                        Description = "Omit this. The assistant is bound to a single database; naming another one is rejected.",
                        Required = false
                    },
                    new()
                    {
                        Name = "maxRows",
                        Type = "integer",
                        Description = "Maximum rows returned per result set (default 200, at most 5000).",
                        Required = false
                    }
                }
            }
        };
    }
}
