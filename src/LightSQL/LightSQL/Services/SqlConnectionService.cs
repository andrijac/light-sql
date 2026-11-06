using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using LightSQL.Models;
using LightSQL.Services.Database;

namespace LightSQL.Services
{
    /// <summary>
    /// Default implementation of <see cref="ISqlConnectionService"/>. It owns the
    /// connection lifetime (open / close / transactions) over the ADO.NET base types
    /// (<see cref="DbConnection"/>, <see cref="DbCommand"/>, <see cref="DbDataReader"/>)
    /// and delegates every engine-specific decision to an <see cref="IDatabaseProvider"/>
    /// chosen from the connected profile's engine. A fresh pooled connection is opened per
    /// operation, targeting the requested database, which keeps execution thread-safe
    /// (several query tabs can execute at once) and the UI responsive.
    /// </summary>
    public sealed class SqlConnectionService : ISqlConnectionService
    {
        private readonly IDatabaseProviderRegistry providerRegistry;

        private ConnectionProfile? activeProfile;
        private IDatabaseProvider? activeProvider;
        private string? currentDatabase;
        private string? serverName;

        /// <summary>
        /// Creates the service with the default provider registry (SQL Server, PostgreSQL,
        /// MySQL). Used by the MCP server factory, which needs its own connection state.
        /// </summary>
        public SqlConnectionService()
            : this(new DatabaseProviderRegistry())
        {
        }

        /// <summary>
        /// Creates the service over a provider registry.
        /// </summary>
        /// <param name="providerRegistry">Resolves the provider for a connection's engine.</param>
        public SqlConnectionService(IDatabaseProviderRegistry providerRegistry)
        {
            this.providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
        }

        /// <inheritdoc />
        public bool IsConnected
        {
            get
            {
                return activeProfile is not null;
            }
        }

        /// <inheritdoc />
        public string? ServerName
        {
            get
            {
                return serverName;
            }
        }

        /// <inheritdoc />
        public string? CurrentDatabase
        {
            get
            {
                return currentDatabase;
            }
        }

        /// <inheritdoc />
        public DatabaseEngine? Engine
        {
            get
            {
                return activeProfile?.Engine;
            }
        }

        /// <inheritdoc />
        public ISqlDialect? Dialect
        {
            get
            {
                return activeProvider?.Dialect;
            }
        }

        /// <inheritdoc />
        public string? AdministrativeDatabase
        {
            get
            {
                return activeProvider?.AdministrativeDatabase;
            }
        }

        /// <inheritdoc />
        public async Task<string> ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            IDatabaseProvider provider = providerRegistry.Get(profile.Engine);
            string connectionString = provider.BuildConnectionString(profile, null);

            await using DbConnection connection = provider.CreateConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Capture the effective current database (the server picks a default when none is set).
            currentDatabase = await provider.GetCurrentDatabaseAsync(connection, cancellationToken);

            activeProfile = profile;
            activeProvider = provider;
            serverName = connection.DataSource;

            return provider.DescribeServer(connection);
        }

        /// <inheritdoc />
        public async Task<string> TestConnectionAsync(ConnectionProfile profile, CancellationToken cancellationToken)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            IDatabaseProvider provider = providerRegistry.Get(profile.Engine);
            string connectionString = provider.BuildConnectionString(profile, null);

            await using DbConnection connection = provider.CreateConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            return provider.DescribeServer(connection);
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            activeProfile = null;
            activeProvider = null;
            currentDatabase = null;
            serverName = null;
        }

        /// <inheritdoc />
        public Task<QueryExecutionResult> ExecuteBatchAsync(string sql, string? database, CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(sql, database, false, cancellationToken);
        }

        /// <inheritdoc />
        public Task<QueryExecutionResult> ExecuteReadOnlyBatchAsync(string sql, string? database, CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(sql, database, true, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken cancellationToken)
        {
            EnsureConnected();

            await using DbConnection connection = await OpenAsync(activeProvider!.AdministrativeDatabase, cancellationToken);

            return await activeProvider!.GetDatabasesAsync(connection, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<SchemaObject>> GetTablesAsync(string database, CancellationToken cancellationToken)
        {
            EnsureConnected();

            await using DbConnection connection = await OpenAsync(database, cancellationToken);

            return await activeProvider!.GetTablesAsync(connection, database, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<SchemaObject>> GetViewsAsync(string database, CancellationToken cancellationToken)
        {
            EnsureConnected();

            await using DbConnection connection = await OpenAsync(database, cancellationToken);

            return await activeProvider!.GetViewsAsync(connection, database, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string database, string schema, string objectName, CancellationToken cancellationToken)
        {
            EnsureConnected();

            await using DbConnection connection = await OpenAsync(database, cancellationToken);

            return await activeProvider!.GetColumnsAsync(connection, database, schema, objectName, cancellationToken);
        }

        /// <inheritdoc />
        public async Task CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken)
        {
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(databaseName) == true)
            {
                throw new ArgumentException("A database name is required.", nameof(databaseName));
            }

            string sql = activeProvider!.Dialect.BuildCreateDatabase(databaseName);

            await using DbConnection connection = await OpenAsync(activeProvider!.AdministrativeDatabase, cancellationToken);

            await using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Works out whether a batch changed its database context (e.g. with USE).
        /// </summary>
        /// <param name="startDatabase">The connection's database before the batch.</param>
        /// <param name="endDatabase">The connection's database after the batch.</param>
        /// <returns>The new database, or null when it did not change.</returns>
        public static string? GetNewDatabaseContext(string? startDatabase, string? endDatabase)
        {
            if (string.IsNullOrWhiteSpace(endDatabase) == true)
            {
                return null;
            }

            // Database names are compared case-insensitively, as on a default-collation server.
            if (string.Equals(startDatabase, endDatabase, StringComparison.OrdinalIgnoreCase) == true)
            {
                return null;
            }

            return endDatabase;
        }

        /// <summary>
        /// Opens a fresh connection for the active profile, targeting a specific database.
        /// </summary>
        /// <param name="database">The database to target, or null for the profile's own.</param>
        /// <param name="cancellationToken">Token to cancel the attempt.</param>
        /// <returns>The opened connection (the caller disposes it).</returns>
        private async Task<DbConnection> OpenAsync(string? database, CancellationToken cancellationToken)
        {
            string connectionString = activeProvider!.BuildConnectionString(activeProfile!, database);
            DbConnection connection = activeProvider!.CreateConnection(connectionString);

            try
            {
                await connection.OpenAsync(cancellationToken);
            }
            catch
            {
                await connection.DisposeAsync();

                throw;
            }

            return connection;
        }

        /// <summary>
        /// Executes a batch on its own pooled connection and collects its result sets,
        /// messages and timing.
        /// </summary>
        /// <param name="sql">The SQL batch to run.</param>
        /// <param name="database">The database to run in, or null for <see cref="CurrentDatabase"/>.</param>
        /// <param name="rollBack">True to run inside a transaction that is always rolled back.</param>
        /// <param name="cancellationToken">Token to cancel execution.</param>
        /// <returns>The execution result.</returns>
        private async Task<QueryExecutionResult> ExecuteCoreAsync(string sql, string? database, bool rollBack, CancellationToken cancellationToken)
        {
            EnsureConnected();

            IDatabaseProvider provider = activeProvider!;
            QueryExecutionResult result = new();
            StringBuilder messages = new();
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Fall back to the connection's default database when none is given.
            string? targetDatabase = string.IsNullOrWhiteSpace(database) == true ? currentDatabase : database;

            try
            {
                string connectionString = provider.BuildConnectionString(activeProfile!, targetDatabase);

                await using DbConnection connection = provider.CreateConnection(connectionString);

                // PRINT output, notices and warnings surface through the provider's message event.
                provider.AttachMessageHandler(connection, line => messages.AppendLine(line));

                await connection.OpenAsync(cancellationToken);

                // The database the batch starts in; compared afterwards to detect USE.
                string startDatabase = connection.Database;

                // Read-only mode: everything the batch does is undone afterwards.
                DbTransaction? transaction = rollBack == true ? await connection.BeginTransactionAsync(cancellationToken) : null;

                try
                {
                    await using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        command.CommandTimeout = 0;
                        command.Transaction = transaction;

                        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

                        do
                        {
                            if (reader.FieldCount > 0)
                            {
                                DataTable table = await ReadResultSetAsync(reader, cancellationToken);
                                result.ResultTables.Add(table);
                            }
                        }
                        while (await reader.NextResultAsync(cancellationToken));

                        int affected = reader.RecordsAffected;
                        result.RecordsAffected = affected < 0 ? 0 : affected;
                        result.IsSuccess = true;
                    }
                }
                finally
                {
                    // The current database follows USE statements (where the engine supports it).
                    // Read it here, while the connection is still open, so a switch is reported even
                    // when a later statement in the batch failed.
                    result.NewDatabaseContext = GetNewDatabaseContext(startDatabase, connection.Database);

                    // The reader is closed by now, so the transaction can be rolled back.
                    await RollBackQuietlyAsync(transaction);
                }

                if (result.ResultTables.Count == 0)
                {
                    messages.AppendLine($"({result.RecordsAffected} row(s) affected)");
                }
            }
            catch (OperationCanceledException)
            {
                // Propagate cancellation so the caller can report it distinctly.
                throw;
            }
            catch (DbException ex)
            {
                if (cancellationToken.IsCancellationRequested == true)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                result.IsSuccess = false;
                AppendErrors(messages, provider, ex);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                messages.AppendLine(ex.Message);
            }

            stopwatch.Stop();
            result.Elapsed = stopwatch.Elapsed;
            result.Messages = messages.ToString().TrimEnd();

            return result;
        }

        /// <summary>
        /// Reads the current result set of a reader into a <see cref="DataTable"/>,
        /// ensuring unique column names.
        /// </summary>
        /// <param name="reader">The open reader positioned on a result set.</param>
        /// <param name="cancellationToken">Token to cancel reading.</param>
        /// <returns>The populated table.</returns>
        private static async Task<DataTable> ReadResultSetAsync(DbDataReader reader, CancellationToken cancellationToken)
        {
            DataTable table = new();
            HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < reader.FieldCount; i++)
            {
                string columnName = MakeUniqueColumnName(reader.GetName(i), i, usedNames);
                Type columnType = reader.GetFieldType(i) ?? typeof(object);

                table.Columns.Add(columnName, columnType);
            }

            while (await reader.ReadAsync(cancellationToken) == true)
            {
                DataRow row = table.NewRow();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader.IsDBNull(i) == true ? DBNull.Value : reader.GetValue(i);
                }

                table.Rows.Add(row);
            }

            return table;
        }

        /// <summary>
        /// Produces a unique, non-empty column name for a result column.
        /// </summary>
        /// <param name="rawName">The raw column name from the reader (may be empty).</param>
        /// <param name="ordinal">The column's zero-based position.</param>
        /// <param name="usedNames">Names already assigned in this table.</param>
        /// <returns>A unique column name.</returns>
        private static string MakeUniqueColumnName(string rawName, int ordinal, HashSet<string> usedNames)
        {
            string baseName = string.IsNullOrWhiteSpace(rawName) == true ? $"Column{ordinal + 1}" : rawName;
            string candidate = baseName;
            int suffix = 1;

            while (usedNames.Add(candidate) == false)
            {
                candidate = $"{baseName}_{suffix}";
                suffix++;
            }

            return candidate;
        }

        /// <summary>
        /// Appends a database error's text to the message buffer, preferring the provider's
        /// rich formatting and falling back to the exception message.
        /// </summary>
        /// <param name="messages">The message buffer.</param>
        /// <param name="provider">The active provider.</param>
        /// <param name="exception">The database exception.</param>
        private static void AppendErrors(StringBuilder messages, IDatabaseProvider provider, DbException exception)
        {
            bool any = false;

            foreach (string line in provider.GetErrorMessages(exception))
            {
                messages.AppendLine(line);
                any = true;
            }

            if (any == false)
            {
                messages.AppendLine(exception.Message);
            }
        }

        /// <summary>
        /// Rolls back and disposes a read-only batch's transaction. A transaction the
        /// server already ended (e.g. after a severe error) cannot be rolled back
        /// again; that is ignored, as there is nothing left to undo.
        /// </summary>
        /// <param name="transaction">The transaction, or null when none was used.</param>
        /// <returns>A task that completes when the rollback has been attempted.</returns>
        private static async Task RollBackQuietlyAsync(DbTransaction? transaction)
        {
            if (transaction is null)
            {
                return;
            }

            try
            {
                if (transaction.Connection is not null)
                {
                    await transaction.RollbackAsync();
                }
            }
            catch (InvalidOperationException)
            {
                // Already completed by the server.
            }
            catch (DbException)
            {
                // The connection is broken; the server rolls the transaction back itself.
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        }

        /// <summary>
        /// Throws when no connection has been established.
        /// </summary>
        private void EnsureConnected()
        {
            if (activeProfile is null || activeProvider is null)
            {
                throw new InvalidOperationException("No active connection. Connect to a server first.");
            }
        }
    }
}
