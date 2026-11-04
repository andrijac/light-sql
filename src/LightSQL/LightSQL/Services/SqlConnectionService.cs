using System.Data;
using System.Diagnostics;
using System.Text;
using LightSQL.Models;
using Microsoft.Data.SqlClient;

namespace LightSQL.Services
{
    /// <summary>
    /// Default implementation of <see cref="ISqlConnectionService"/> using
    /// Microsoft.Data.SqlClient. A fresh pooled connection is opened per operation,
    /// targeting the requested database, which keeps execution thread-safe (several
    /// query tabs can execute at once) and the UI responsive.
    /// </summary>
    public sealed class SqlConnectionService : ISqlConnectionService
    {
        private ConnectionProfile? activeProfile;
        private string? currentDatabase;
        private string? serverName;

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
        public async Task<string> ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            string connectionString = ConnectionStringFactory.Build(profile);

            await using SqlConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Capture the effective current database (the server picks a default when none is set).
            using (SqlCommand command = new("SELECT DB_NAME()", connection))
            {
                object? result = await command.ExecuteScalarAsync(cancellationToken);
                currentDatabase = result as string;
            }

            activeProfile = profile;
            serverName = connection.DataSource;

            return $"{connection.DataSource} (SQL Server {connection.ServerVersion})";
        }

        /// <inheritdoc />
        public async Task<string> TestConnectionAsync(ConnectionProfile profile, CancellationToken cancellationToken)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            string connectionString = ConnectionStringFactory.Build(profile);

            await using SqlConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken);

            return $"{connection.DataSource} (SQL Server {connection.ServerVersion})";
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            activeProfile = null;
            currentDatabase = null;
            serverName = null;
        }

        /// <inheritdoc />
        public async Task<QueryExecutionResult> ExecuteBatchAsync(string sql, string? database, CancellationToken cancellationToken)
        {
            EnsureConnected();

            QueryExecutionResult result = new();
            StringBuilder messages = new();
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Fall back to the connection's default database when none is given.
            string? targetDatabase = string.IsNullOrWhiteSpace(database) == true ? currentDatabase : database;

            try
            {
                string connectionString = ConnectionStringFactory.Build(activeProfile!, targetDatabase);

                await using SqlConnection connection = new(connectionString);

                // PRINT output and warnings surface through InfoMessage.
                connection.InfoMessage += (sender, args) => AppendInfoMessages(messages, args);

                await connection.OpenAsync(cancellationToken);

                // The database the batch starts in; compared afterwards to detect USE.
                string startDatabase = connection.Database;

                try
                {
                    await using SqlCommand command = new(sql, connection)
                    {
                        CommandTimeout = 0
                    };

                    await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

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
                finally
                {
                    // SqlConnection.Database follows USE statements. Read it here, while
                    // the connection is still open, so a switch is reported even when a
                    // later statement in the batch failed.
                    result.NewDatabaseContext = GetNewDatabaseContext(startDatabase, connection.Database);
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
            catch (SqlException ex)
            {
                if (cancellationToken.IsCancellationRequested == true)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                result.IsSuccess = false;
                AppendSqlErrors(messages, ex);
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

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken cancellationToken)
        {
            EnsureConnected();

            const string query = "SELECT name FROM sys.databases WHERE state = 0 ORDER BY name";
            List<string> databases = new();

            string connectionString = ConnectionStringFactory.Build(activeProfile!, "master");

            await using SqlConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new(query, connection);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                databases.Add(reader.GetString(0));
            }

            return databases;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<SchemaObject>> GetTablesAsync(string database, CancellationToken cancellationToken)
        {
            const string query = @"SELECT s.name AS SchemaName, t.name AS ObjectName
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                ORDER BY s.name, t.name";

            return await GetSchemaObjectsAsync(database, query, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<SchemaObject>> GetViewsAsync(string database, CancellationToken cancellationToken)
        {
            const string query = @"SELECT s.name AS SchemaName, v.name AS ObjectName
                FROM sys.views v
                INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
                ORDER BY s.name, v.name";

            return await GetSchemaObjectsAsync(database, query, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string database, string schema, string objectName, CancellationToken cancellationToken)
        {
            EnsureConnected();

            const string query = @"SELECT c.name AS ColumnName, ty.name AS TypeName,
                    c.max_length AS MaxLength, c.precision AS Precision, c.scale AS Scale, c.is_nullable AS IsNullable
                FROM sys.columns c
                INNER JOIN sys.objects o ON c.object_id = o.object_id
                INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                WHERE s.name = @schema AND o.name = @object
                ORDER BY c.column_id";

            List<ColumnInfo> columns = new();

            string connectionString = ConnectionStringFactory.Build(activeProfile!, database);

            await using SqlConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new(query, connection);
            command.Parameters.AddWithValue("@schema", schema);
            command.Parameters.AddWithValue("@object", objectName);

            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                string typeName = reader.GetString(1);
                short maxLength = reader.GetInt16(2);
                byte precision = reader.GetByte(3);
                byte scale = reader.GetByte(4);

                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = FormatDataType(typeName, maxLength, precision, scale),
                    IsNullable = reader.GetBoolean(5)
                });
            }

            return columns;
        }

        /// <inheritdoc />
        public async Task CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken)
        {
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(databaseName) == true)
            {
                throw new ArgumentException("A database name is required.", nameof(databaseName));
            }

            // The database name cannot be parameterized; escape it and bracket-quote it.
            string escaped = databaseName.Replace("]", "]]");
            string sql = $"CREATE DATABASE [{escaped}]";

            string connectionString = ConnectionStringFactory.Build(activeProfile!, "master");

            await using SqlConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new(sql, connection);
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
        /// Runs a schema-object listing query against a specific database.
        /// </summary>
        /// <param name="database">The database to inspect.</param>
        /// <param name="query">The listing query returning SchemaName and ObjectName.</param>
        /// <param name="cancellationToken">Token to cancel the lookup.</param>
        /// <returns>The listed objects.</returns>
        private async Task<IReadOnlyList<SchemaObject>> GetSchemaObjectsAsync(string database, string query, CancellationToken cancellationToken)
        {
            EnsureConnected();

            List<SchemaObject> objects = new();

            string connectionString = ConnectionStringFactory.Build(activeProfile!, database);

            await using SqlConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new(query, connection);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                objects.Add(new SchemaObject
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1)
                });
            }

            return objects;
        }

        /// <summary>
        /// Reads the current result set of a reader into a <see cref="DataTable"/>,
        /// ensuring unique column names.
        /// </summary>
        /// <param name="reader">The open reader positioned on a result set.</param>
        /// <param name="cancellationToken">Token to cancel reading.</param>
        /// <returns>The populated table.</returns>
        private static async Task<DataTable> ReadResultSetAsync(SqlDataReader reader, CancellationToken cancellationToken)
        {
            DataTable table = new();
            HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < reader.FieldCount; i++)
            {
                string columnName = MakeUniqueColumnName(reader.GetName(i), i, usedNames);
                Type columnType = reader.GetFieldType(i) ?? typeof(object);

                table.Columns.Add(columnName, columnType);
            }

            while (await reader.ReadAsync(cancellationToken))
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
        /// Formats a SQL type name with its length/precision for display.
        /// </summary>
        /// <param name="typeName">The base type name.</param>
        /// <param name="maxLength">The max_length value from sys.columns.</param>
        /// <param name="precision">The numeric precision.</param>
        /// <param name="scale">The numeric scale.</param>
        /// <returns>A display string such as "nvarchar(50)" or "decimal(18,2)".</returns>
        private static string FormatDataType(string typeName, short maxLength, byte precision, byte scale)
        {
            switch (typeName.ToLowerInvariant())
            {
                case "nvarchar":
                case "nchar":
                {
                    // Unicode types store two bytes per character; -1 means MAX.
                    string length = maxLength == -1 ? "max" : (maxLength / 2).ToString();

                    return $"{typeName}({length})";
                }

                case "varchar":
                case "char":
                case "varbinary":
                case "binary":
                {
                    string length = maxLength == -1 ? "max" : maxLength.ToString();

                    return $"{typeName}({length})";
                }

                case "decimal":
                case "numeric":
                {
                    return $"{typeName}({precision},{scale})";
                }

                default:
                {
                    return typeName;
                }
            }
        }

        /// <summary>
        /// Appends PRINT/warning text from an InfoMessage event to the message buffer.
        /// </summary>
        /// <param name="messages">The message buffer.</param>
        /// <param name="args">The info-message event data.</param>
        private static void AppendInfoMessages(StringBuilder messages, SqlInfoMessageEventArgs args)
        {
            foreach (SqlError error in args.Errors)
            {
                messages.AppendLine(error.Message);
            }
        }

        /// <summary>
        /// Appends the errors of a <see cref="SqlException"/> to the message buffer.
        /// </summary>
        /// <param name="messages">The message buffer.</param>
        /// <param name="exception">The SQL exception.</param>
        private static void AppendSqlErrors(StringBuilder messages, SqlException exception)
        {
            foreach (SqlError error in exception.Errors)
            {
                messages.AppendLine($"Msg {error.Number}, Level {error.Class}, State {error.State}, Line {error.LineNumber}");
                messages.AppendLine(error.Message);
            }
        }

        /// <summary>
        /// Throws when no connection has been established.
        /// </summary>
        private void EnsureConnected()
        {
            if (activeProfile is null)
            {
                throw new InvalidOperationException("No active connection. Connect to a server first.");
            }
        }
    }
}
