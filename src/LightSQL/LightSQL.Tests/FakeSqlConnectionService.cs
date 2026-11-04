using System.Data;
using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.Tests
{
    /// <summary>
    /// In-memory <see cref="ISqlConnectionService"/> for view-model tests. Records
    /// every executed batch and lets a test control how execution behaves.
    /// </summary>
    public sealed class FakeSqlConnectionService : ISqlConnectionService
    {
        /// <summary>
        /// Every batch passed to <see cref="ExecuteBatchAsync"/>, as (sql, database).
        /// </summary>
        public List<(string Sql, string? Database)> ExecutedBatches { get; } = new();

        /// <summary>
        /// The databases returned by <see cref="GetDatabasesAsync"/>.
        /// </summary>
        public List<string> DatabaseNames { get; } = new() { "master", "Sales", "HR" };

        /// <summary>
        /// Optional custom execution behaviour. When null, a result with one table
        /// containing one row is returned.
        /// </summary>
        public Func<string, string?, CancellationToken, Task<QueryExecutionResult>>? ExecuteHandler { get; set; }

        /// <inheritdoc />
        public bool IsConnected { get; set; }

        /// <inheritdoc />
        public string? ServerName { get; set; }

        /// <inheritdoc />
        public string? CurrentDatabase { get; set; }

        /// <summary>
        /// Builds a successful result with one single-column table of the given row count.
        /// </summary>
        /// <param name="rowCount">The number of rows in the table.</param>
        /// <returns>The result.</returns>
        public static QueryExecutionResult CreateResult(int rowCount)
        {
            DataTable table = new();
            table.Columns.Add("Value", typeof(int));

            for (int i = 0; i < rowCount; i++)
            {
                table.Rows.Add(i);
            }

            QueryExecutionResult result = new()
            {
                IsSuccess = true,
                Elapsed = TimeSpan.FromMilliseconds(5)
            };

            result.ResultTables.Add(table);

            return result;
        }

        /// <inheritdoc />
        public Task<string> ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken)
        {
            IsConnected = true;
            ServerName = profile.Server;
            CurrentDatabase ??= "master";

            return Task.FromResult($"{profile.Server} (fake)");
        }

        /// <inheritdoc />
        public Task<string> TestConnectionAsync(ConnectionProfile profile, CancellationToken cancellationToken)
        {
            return Task.FromResult($"{profile.Server} (fake)");
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            IsConnected = false;
            ServerName = null;
            CurrentDatabase = null;
        }

        /// <inheritdoc />
        public Task<QueryExecutionResult> ExecuteBatchAsync(string sql, string? database, CancellationToken cancellationToken)
        {
            ExecutedBatches.Add((sql, database));

            if (ExecuteHandler is not null)
            {
                return ExecuteHandler(sql, database, cancellationToken);
            }

            return Task.FromResult(CreateResult(1));
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<string> names = DatabaseNames.ToList();

            return Task.FromResult(names);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<SchemaObject>> GetTablesAsync(string database, CancellationToken cancellationToken)
        {
            IReadOnlyList<SchemaObject> objects = new List<SchemaObject>();

            return Task.FromResult(objects);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<SchemaObject>> GetViewsAsync(string database, CancellationToken cancellationToken)
        {
            IReadOnlyList<SchemaObject> objects = new List<SchemaObject>();

            return Task.FromResult(objects);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string database, string schema, string objectName, CancellationToken cancellationToken)
        {
            IReadOnlyList<ColumnInfo> columns = new List<ColumnInfo>();

            return Task.FromResult(columns);
        }

        /// <inheritdoc />
        public Task CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken)
        {
            DatabaseNames.Add(databaseName);

            return Task.CompletedTask;
        }
    }
}
