using System.Text.Json.Nodes;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.Services.Mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the MCP tools (list_databases, list_tables, describe_table,
    /// execute_query) against the fake SQL service.
    /// </summary>
    public class McpDatabaseToolsTests
    {
        private readonly FakeSqlConnectionService sqlService = new() { IsConnected = true, CurrentDatabase = "master" };
        private readonly List<McpToolCall> calls = new();

        /// <summary>
        /// The access mode the tools see; read-only unless a test changes it.
        /// </summary>
        private McpAccessMode accessMode = McpAccessMode.ReadOnly;

        [Theory]
        [InlineData("Orders", null, "dbo", "Orders")]
        [InlineData("sales.Orders", null, "sales", "Orders")]
        [InlineData("[sales].[Order Details]", null, "sales", "Order Details")]
        [InlineData("[my.schema].[t]", null, "my.schema", "t")]
        [InlineData("Orders", "hr", "hr", "Orders")]
        [InlineData("sales.Orders", "hr", "hr", "Orders")]
        [InlineData("[a]]b]", null, "dbo", "a]b")]
        public void ParseObjectName_SplitsSchemaAndName(string table, string? schema, string expectedSchema, string expectedName)
        {
            (string Schema, string Name) parsed = McpDatabaseTools.ParseObjectName(table, schema);

            Assert.Equal(expectedSchema, parsed.Schema);
            Assert.Equal(expectedName, parsed.Name);
        }

        [Fact]
        public async Task ExecuteQuery_RunsInDefaultDatabase_AndLogsTheCall()
        {
            McpDatabaseTools tools = CreateTools("Sales");

            CallToolResult result = await tools.ExecuteQueryAsync("SELECT 1");

            Assert.NotEqual(true, result.IsError);
            (string Sql, string? Database) batch = Assert.Single(sqlService.ExecutedBatches);
            Assert.Equal("SELECT 1", batch.Sql);
            Assert.Equal("Sales", batch.Database);

            McpToolCall call = Assert.Single(calls);
            Assert.Equal("execute_query", call.ToolName);
            Assert.Equal("SELECT 1", call.Detail);
            Assert.Equal("Sales", call.Database);
            Assert.True(call.IsSuccess);
            Assert.Equal("1 result set(s), 1 row(s)", call.Outcome);

            JsonNode root = JsonNode.Parse(GetText(result))!;
            Assert.True(root["success"]!.GetValue<bool>());
        }

        [Fact]
        public async Task ReadOnly_RunsReadingQueriesInARolledBackTransaction()
        {
            McpDatabaseTools tools = CreateTools(null);

            CallToolResult result = await tools.ExecuteQueryAsync("SELECT * FROM Orders");

            Assert.NotEqual(true, result.IsError);
            Assert.Equal("SELECT * FROM Orders", Assert.Single(sqlService.ReadOnlyBatches));
        }

        [Fact]
        public async Task ReadOnly_RejectsDataChanges_WithoutRunningThem()
        {
            McpDatabaseTools tools = CreateTools(null);

            CallToolResult result = await tools.ExecuteQueryAsync("INSERT INTO Orders (Id) VALUES (1)");

            Assert.True(result.IsError);
            Assert.Contains("read-only", GetText(result));
            Assert.Contains("INSERT", GetText(result));
            Assert.Empty(sqlService.ExecutedBatches);

            McpToolCall call = Assert.Single(calls);
            Assert.False(call.IsSuccess);
            Assert.Equal("Blocked (read-only): INSERT", call.Outcome);
        }

        [Fact]
        public async Task ReadWrite_RunsChangesNormally()
        {
            accessMode = McpAccessMode.ReadWrite;
            McpDatabaseTools tools = CreateTools(null);

            CallToolResult result = await tools.ExecuteQueryAsync("DELETE FROM Orders WHERE Id = 1");

            Assert.NotEqual(true, result.IsError);
            Assert.Equal("DELETE FROM Orders WHERE Id = 1", Assert.Single(sqlService.ExecutedBatches).Sql);
            Assert.Empty(sqlService.ReadOnlyBatches);
        }

        [Fact]
        public async Task AccessMode_IsReadOnEveryCall()
        {
            McpDatabaseTools tools = CreateTools(null);

            accessMode = McpAccessMode.ReadWrite;
            CallToolResult allowed = await tools.ExecuteQueryAsync("UPDATE Orders SET Total = 0 WHERE Id = 1");

            accessMode = McpAccessMode.ReadOnly;
            CallToolResult blocked = await tools.ExecuteQueryAsync("UPDATE Orders SET Total = 0 WHERE Id = 1");

            Assert.NotEqual(true, allowed.IsError);
            Assert.True(blocked.IsError);
            Assert.Single(sqlService.ExecutedBatches);
        }

        [Fact]
        public async Task ExecuteQuery_UsesConnectionDefault_WhenNoDefaultDatabase()
        {
            McpDatabaseTools tools = CreateTools(null);

            await tools.ExecuteQueryAsync("SELECT 1");

            Assert.Equal("master", sqlService.ExecutedBatches[0].Database);
        }

        [Fact]
        public async Task ExecuteQuery_UsesDatabaseArgument()
        {
            McpDatabaseTools tools = CreateTools("Sales");

            await tools.ExecuteQueryAsync("SELECT 1", "HR");

            Assert.Equal("HR", sqlService.ExecutedBatches[0].Database);
        }

        [Fact]
        public async Task ExecuteQuery_ReturnsErrorResult_WhenTheBatchFails()
        {
            sqlService.ExecuteHandler = (sql, database, token) => Task.FromResult(new QueryExecutionResult
            {
                IsSuccess = false,
                Messages = "Msg 208, Level 16, State 1, Line 1\nInvalid object name 'Nope'."
            });
            McpDatabaseTools tools = CreateTools(null);

            CallToolResult result = await tools.ExecuteQueryAsync("SELECT * FROM Nope");

            Assert.True(result.IsError);
            Assert.Contains("Invalid object name", GetText(result));
            Assert.False(calls[0].IsSuccess);
            Assert.Equal("Invalid object name 'Nope'.", calls[0].Outcome);
        }

        [Fact]
        public async Task ExecuteQuery_ReturnsErrorResult_WhenTheServiceThrows()
        {
            sqlService.ExecuteHandler = (sql, database, token) => throw new InvalidOperationException("Login failed.");
            McpDatabaseTools tools = CreateTools(null);

            CallToolResult result = await tools.ExecuteQueryAsync("SELECT 1");

            Assert.True(result.IsError);
            Assert.Equal("Login failed.", GetText(result));
            Assert.False(calls[0].IsSuccess);
        }

        [Fact]
        public async Task ExecuteQuery_RejectsEmptySql()
        {
            McpDatabaseTools tools = CreateTools(null);

            CallToolResult result = await tools.ExecuteQueryAsync("   ");

            Assert.True(result.IsError);
            Assert.Empty(sqlService.ExecutedBatches);
        }

        [Fact]
        public async Task ExecuteQuery_AppliesTheRowLimit()
        {
            sqlService.ExecuteHandler = (sql, database, token) => Task.FromResult(FakeSqlConnectionService.CreateResult(50));
            McpDatabaseTools tools = CreateTools(null);

            CallToolResult result = await tools.ExecuteQueryAsync("SELECT 1", maxRows: 5);

            JsonNode resultSet = JsonNode.Parse(GetText(result))!["resultSets"]![0]!;
            Assert.Equal(5, resultSet["rows"]!.AsArray().Count);
            Assert.True(resultSet["truncated"]!.GetValue<bool>());
        }

        [Fact]
        public async Task ExecuteQuery_LogsAndRethrowsCancellation()
        {
            sqlService.ExecuteHandler = (sql, database, token) => throw new OperationCanceledException();
            McpDatabaseTools tools = CreateTools(null);

            await Assert.ThrowsAsync<OperationCanceledException>(() => tools.ExecuteQueryAsync("SELECT 1"));

            Assert.Equal("Cancelled by the client", Assert.Single(calls).Outcome);
        }

        [Fact]
        public async Task ListDatabases_ReturnsTheServerDatabases()
        {
            McpDatabaseTools tools = CreateTools(null);

            CallToolResult result = await tools.ListDatabasesAsync();

            JsonArray names = JsonNode.Parse(GetText(result))!.AsArray();
            Assert.Equal(new[] { "master", "Sales", "HR" }, names.Select(name => name!.GetValue<string>()));
            Assert.Equal("3 database(s)", calls[0].Outcome);
        }

        [Fact]
        public async Task ListTables_ListsTheDefaultDatabase()
        {
            sqlService.Tables.Add(new SchemaObject { Schema = "dbo", Name = "Orders" });
            McpDatabaseTools tools = CreateTools("Sales");

            CallToolResult result = await tools.ListTablesAsync();

            JsonNode root = JsonNode.Parse(GetText(result))!;
            Assert.Equal("Sales", root["database"]!.GetValue<string>());
            Assert.Equal("Orders", root["objects"]![0]!["name"]!.GetValue<string>());
            Assert.Equal("1 table(s), 0 view(s)", calls[0].Outcome);
        }

        [Fact]
        public async Task DescribeTable_LooksUpSchemaQualifiedName()
        {
            sqlService.Columns.Add(new ColumnInfo { Name = "Id", DataType = "int", IsNullable = false });
            McpDatabaseTools tools = CreateTools("Sales");

            CallToolResult result = await tools.DescribeTableAsync("sales.Orders");

            Assert.NotEqual(true, result.IsError);
            Assert.Equal(("Sales", "sales", "Orders"), sqlService.ColumnLookups[0]);
            Assert.Equal("sales.Orders", calls[0].Detail);
        }

        [Fact]
        public async Task DescribeTable_ReturnsError_WhenTableIsUnknown()
        {
            McpDatabaseTools tools = CreateTools(null);

            CallToolResult result = await tools.DescribeTableAsync("Missing");

            Assert.True(result.IsError);
            Assert.Contains("dbo.Missing", GetText(result));
        }

        [Fact]
        public void BuildInstructions_NamesEngineServerAndDefaultDatabase()
        {
            string instructions = McpServerHostFactory.BuildInstructions("PostgreSQL", "pg01", "Sales");

            Assert.Contains("pg01", instructions);
            Assert.Contains("'Sales'", instructions);
            Assert.Contains("PostgreSQL", instructions);
        }

        /// <summary>
        /// Creates the tools over the fake service, recording calls in <see cref="calls"/>.
        /// </summary>
        /// <param name="defaultDatabase">The default database, or null.</param>
        /// <returns>The tools.</returns>
        private McpDatabaseTools CreateTools(string? defaultDatabase)
        {
            return new McpDatabaseTools(sqlService, new SqlStatementAnalyzer(), defaultDatabase, () => accessMode, calls.Add);
        }

        /// <summary>
        /// Returns the text of a tool result's single text block.
        /// </summary>
        /// <param name="result">The tool result.</param>
        /// <returns>The text.</returns>
        private static string GetText(CallToolResult result)
        {
            return Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        }
    }
}
