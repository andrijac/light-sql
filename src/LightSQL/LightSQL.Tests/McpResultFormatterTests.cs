using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using LightSQL.Models;
using LightSQL.Services.Mcp;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the JSON text the MCP tools return to AI clients.
    /// </summary>
    public class McpResultFormatterTests
    {
        [Theory]
        [InlineData(0, McpResultFormatter.DefaultMaxRows)]
        [InlineData(-5, McpResultFormatter.DefaultMaxRows)]
        [InlineData(10, 10)]
        [InlineData(1_000_000, McpResultFormatter.MaxRowsLimit)]
        public void ClampMaxRows_KeepsLimitInRange(int requested, int expected)
        {
            Assert.Equal(expected, McpResultFormatter.ClampMaxRows(requested));
        }

        [Fact]
        public void FormatExecutionResult_IncludesColumnsRowsAndMessages()
        {
            DataTable table = new();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Rows.Add(1, "Ana");
            table.Rows.Add(2, DBNull.Value);

            QueryExecutionResult result = new() { IsSuccess = true, Messages = "done" };
            result.ResultTables.Add(table);

            JsonNode root = Parse(McpResultFormatter.FormatExecutionResult(result, "Sales", 100));

            Assert.True(root["success"]!.GetValue<bool>());
            Assert.Equal("Sales", root["database"]!.GetValue<string>());
            Assert.Equal("done", root["messages"]!.GetValue<string>());

            JsonNode resultSet = root["resultSets"]![0]!;
            Assert.Equal("Id", resultSet["columns"]![0]!["name"]!.GetValue<string>());
            Assert.Equal("Int32", resultSet["columns"]![0]!["type"]!.GetValue<string>());
            Assert.Equal(1, resultSet["rows"]![0]![0]!.GetValue<long>());
            Assert.Equal("Ana", resultSet["rows"]![0]![1]!.GetValue<string>());
            Assert.Null(resultSet["rows"]![1]![1]);
            Assert.Equal(2, resultSet["totalRows"]!.GetValue<int>());
            Assert.False(resultSet["truncated"]!.GetValue<bool>());
        }

        [Fact]
        public void FormatExecutionResult_TruncatesRowsBeyondTheLimit()
        {
            DataTable table = new();
            table.Columns.Add("Value", typeof(int));

            for (int i = 0; i < 10; i++)
            {
                table.Rows.Add(i);
            }

            QueryExecutionResult result = new() { IsSuccess = true };
            result.ResultTables.Add(table);

            JsonNode resultSet = Parse(McpResultFormatter.FormatExecutionResult(result, null, 3))["resultSets"]![0]!;

            Assert.Equal(3, resultSet["rows"]!.AsArray().Count);
            Assert.Equal(10, resultSet["totalRows"]!.GetValue<int>());
            Assert.True(resultSet["truncated"]!.GetValue<bool>());
        }

        [Fact]
        public void FormatExecutionResult_ExplainsThatUseDoesNotPersist()
        {
            QueryExecutionResult result = new() { IsSuccess = true, NewDatabaseContext = "HR" };

            JsonNode root = Parse(McpResultFormatter.FormatExecutionResult(result, "master", 10));

            Assert.Contains("USE", root["note"]!.GetValue<string>());
        }

        [Fact]
        public void FormatExecutionResult_ReportsFailure()
        {
            QueryExecutionResult result = new() { IsSuccess = false, Messages = "Invalid object name 'Nope'." };

            JsonNode root = Parse(McpResultFormatter.FormatExecutionResult(result, "master", 10));

            Assert.False(root["success"]!.GetValue<bool>());
            Assert.Empty(root["resultSets"]!.AsArray());
            Assert.Null(root["note"]);
        }

        [Fact]
        public void ToJsonValue_ConvertsCommonSqlTypes()
        {
            Assert.Null(McpResultFormatter.ToJsonValue(DBNull.Value));
            Assert.Null(McpResultFormatter.ToJsonValue(null));
            Assert.True(McpResultFormatter.ToJsonValue(true)!.GetValue<bool>());
            Assert.Equal(42L, McpResultFormatter.ToJsonValue((short)42)!.GetValue<long>());
            Assert.Equal(12.5m, McpResultFormatter.ToJsonValue(12.5m)!.GetValue<decimal>());
            Assert.Equal("2026-11-06T10:30:00.0000000", McpResultFormatter.ToJsonValue(new DateTime(2026, 11, 6, 10, 30, 0))!.GetValue<string>());
            Assert.Equal("0x0AFF", McpResultFormatter.ToJsonValue(new byte[] { 0x0A, 0xFF })!.GetValue<string>());

            Guid id = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");
            Assert.Equal(id.ToString(), McpResultFormatter.ToJsonValue(id)!.GetValue<string>());
        }

        [Fact]
        public void ToJsonValue_SendsNonFiniteNumbersAsText()
        {
            Assert.Equal("NaN", McpResultFormatter.ToJsonValue(double.NaN)!.GetValue<string>());
        }

        [Fact]
        public void ToJsonValue_CutsLongText()
        {
            string longText = new('x', McpResultFormatter.MaxCellLength + 50);

            string value = McpResultFormatter.ToJsonValue(longText)!.GetValue<string>();

            Assert.Equal(McpResultFormatter.MaxCellLength + 3, value.Length);
            Assert.EndsWith("...", value);
        }

        [Fact]
        public void FormatObjects_ListsTablesThenViews()
        {
            List<SchemaObject> tables = new() { new SchemaObject { Schema = "dbo", Name = "Orders" } };
            List<SchemaObject> views = new() { new SchemaObject { Schema = "sales", Name = "Totals" } };

            JsonArray objects = Parse(McpResultFormatter.FormatObjects("Sales", tables, views))["objects"]!.AsArray();

            Assert.Equal(2, objects.Count);
            Assert.Equal("table", objects[0]!["type"]!.GetValue<string>());
            Assert.Equal("Totals", objects[1]!["name"]!.GetValue<string>());
            Assert.Equal("view", objects[1]!["type"]!.GetValue<string>());
        }

        [Fact]
        public void FormatColumns_IncludesTypeAndNullability()
        {
            List<ColumnInfo> columns = new() { new ColumnInfo { Name = "Total", DataType = "decimal(18,2)", IsNullable = true } };

            JsonNode column = Parse(McpResultFormatter.FormatColumns("Sales", "dbo", "Orders", columns))["columns"]![0]!;

            Assert.Equal("Total", column["name"]!.GetValue<string>());
            Assert.Equal("decimal(18,2)", column["type"]!.GetValue<string>());
            Assert.True(column["nullable"]!.GetValue<bool>());
        }

        [Fact]
        public void FormatNames_ReturnsJsonArray()
        {
            string json = McpResultFormatter.FormatNames(new[] { "master", "Sales" });

            Assert.Equal(new[] { "master", "Sales" }, JsonSerializer.Deserialize<string[]>(json));
        }

        /// <summary>
        /// Parses formatter output.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <returns>The root node.</returns>
        private static JsonNode Parse(string json)
        {
            return JsonNode.Parse(json)!;
        }
    }
}
