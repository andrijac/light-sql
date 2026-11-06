using System.Data;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.Services.Database;

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
        /// The batches passed to <see cref="ExecuteReadOnlyBatchAsync"/> (they are also
        /// recorded in <see cref="ExecutedBatches"/>).
        /// </summary>
        public List<string> ReadOnlyBatches { get; } = new();

        /// <summary>
        /// The databases returned by <see cref="GetDatabasesAsync"/>.
        /// </summary>
        public List<string> DatabaseNames { get; } = new() { "master", "Sales", "HR" };

        /// <summary>
        /// The tables returned by <see cref="GetTablesAsync"/>.
        /// </summary>
        public List<SchemaObject> Tables { get; } = new();

        /// <summary>
        /// The columns returned by <see cref="GetColumnsAsync"/> (for any table).
        /// </summary>
        public List<ColumnInfo> Columns { get; } = new();

        /// <summary>
        /// Every (database, schema, object) passed to <see cref="GetColumnsAsync"/>.
        /// </summary>
        public List<(string Database, string Schema, string ObjectName)> ColumnLookups { get; } = new();

        /// <summary>
        /// When set, <see cref="ConnectAsync"/> throws this exception (e.g. a failed login).
        /// </summary>
        public Exception? ConnectFailure { get; set; }

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

        /// <inheritdoc />
        public DatabaseEngine? Engine { get; set; } = DatabaseEngine.SqlServer;

        /// <inheritdoc />
        public ISqlDialect? Dialect { get; set; } = new SqlServerDialect();

        /// <inheritdoc />
        public string? AdministrativeDatabase { get; set; } = "master";

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
            if (ConnectFailure is not null)
            {
                return Task.FromException<string>(ConnectFailure);
            }

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
        public Task<QueryExecutionResult> ExecuteReadOnlyBatchAsync(string sql, string? database, CancellationToken cancellationToken)
        {
            ReadOnlyBatches.Add(sql);

            return ExecuteBatchAsync(sql, database, cancellationToken);
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
            IReadOnlyList<SchemaObject> objects = Tables.ToList();

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
            ColumnLookups.Add((database, schema, objectName));

            IReadOnlyList<ColumnInfo> columns = Columns.ToList();

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
