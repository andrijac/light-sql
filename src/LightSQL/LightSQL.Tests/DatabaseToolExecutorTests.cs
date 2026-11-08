using System.Text.Json.Nodes;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.Services.Tools;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the shared <see cref="DatabaseToolExecutor"/> dispatcher (used by the chat agent):
    /// running tools by name with a JSON arguments object, the single-database lock, and
    /// unknown tools.
    /// </summary>
    public class DatabaseToolExecutorTests
    {
        private readonly FakeSqlConnectionService sqlService = new() { IsConnected = true, CurrentDatabase = "master" };

        private McpAccessMode accessMode = McpAccessMode.ReadOnly;

        [Fact]
        public async Task ExecuteAsync_ListTables_ReturnsTheLockedDatabaseObjects()
        {
            sqlService.Tables.Add(new SchemaObject { Schema = "dbo", Name = "Orders" });
            DatabaseToolExecutor executor = CreateExecutor("Sales");

            ToolCallOutcome outcome = await executor.ExecuteAsync("list_tables", new JsonObject(), CancellationToken.None);

            Assert.False(outcome.IsError);
            Assert.Contains("Orders", outcome.Text);
        }

        [Fact]
        public async Task ExecuteAsync_ExecuteQuery_RunsSqlOnTheLockedDatabase()
        {
            accessMode = McpAccessMode.ReadWrite;
            DatabaseToolExecutor executor = CreateExecutor("Sales");

            JsonObject arguments = new() { ["sql"] = "SELECT 1" };
            ToolCallOutcome outcome = await executor.ExecuteAsync("execute_query", arguments, CancellationToken.None);

            Assert.False(outcome.IsError);
            Assert.Equal("Sales", Assert.Single(sqlService.ExecutedBatches).Database);
        }

        [Fact]
        public async Task ExecuteAsync_ReadOnly_RejectsDataChanges()
        {
            DatabaseToolExecutor executor = CreateExecutor("Sales");

            JsonObject arguments = new() { ["sql"] = "DELETE FROM Orders" };
            ToolCallOutcome outcome = await executor.ExecuteAsync("execute_query", arguments, CancellationToken.None);

            Assert.True(outcome.IsError);
            Assert.Contains("read-only", outcome.Text);
            Assert.Empty(sqlService.ExecutedBatches);
        }

        [Fact]
        public async Task ExecuteAsync_RejectsForeignDatabaseArgument()
        {
            DatabaseToolExecutor executor = CreateExecutor("Sales");

            JsonObject arguments = new() { ["sql"] = "SELECT 1", ["database"] = "HR" };
            ToolCallOutcome outcome = await executor.ExecuteAsync("execute_query", arguments, CancellationToken.None);

            Assert.True(outcome.IsError);
            Assert.Contains("locked", outcome.Text);
        }

        [Fact]
        public async Task ExecuteAsync_UnknownTool_ReturnsError()
        {
            DatabaseToolExecutor executor = CreateExecutor("Sales");

            ToolCallOutcome outcome = await executor.ExecuteAsync("nope", new JsonObject(), CancellationToken.None);

            Assert.True(outcome.IsError);
            Assert.Contains("Unknown tool", outcome.Text);
        }

        /// <summary>
        /// Creates an executor over the fake service, locked to the given database.
        /// </summary>
        /// <param name="defaultDatabase">The locked database.</param>
        /// <returns>The executor.</returns>
        private DatabaseToolExecutor CreateExecutor(string? defaultDatabase)
        {
            return new DatabaseToolExecutor(sqlService, new SqlStatementAnalyzer(), defaultDatabase, () => accessMode, null);
        }
    }
}
