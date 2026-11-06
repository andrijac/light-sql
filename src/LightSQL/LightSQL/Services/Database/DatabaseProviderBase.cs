using System.Data.Common;
using LightSQL.Models;

namespace LightSQL.Services.Database
{
    /// <summary>
    /// Shared plumbing for <see cref="IDatabaseProvider"/> implementations: generic
    /// reader loops (over <see cref="DbCommand"/> / <see cref="DbDataReader"/>) and the
    /// server description, so each concrete provider only supplies its own SQL, mapping
    /// and connection-string building.
    /// </summary>
    public abstract class DatabaseProviderBase : IDatabaseProvider
    {
        /// <inheritdoc />
        public abstract DatabaseEngine Engine { get; }

        /// <inheritdoc />
        public abstract string DisplayName { get; }

        /// <inheritdoc />
        public abstract ISqlDialect Dialect { get; }

        /// <inheritdoc />
        public abstract bool SupportsWindowsAuthentication { get; }

        /// <inheritdoc />
        public abstract int DefaultPort { get; }

        /// <inheritdoc />
        public abstract string? AdministrativeDatabase { get; }

        /// <summary>
        /// The query that returns the connection's current database as a single scalar
        /// (DB_NAME() / current_database() / DATABASE()).
        /// </summary>
        protected abstract string CurrentDatabaseQuery { get; }

        /// <inheritdoc />
        public abstract DbConnection CreateConnection(string connectionString);

        /// <inheritdoc />
        public abstract string BuildConnectionString(ConnectionProfile profile, string? overrideDatabase);

        /// <inheritdoc />
        public abstract Task<IReadOnlyList<string>> GetDatabasesAsync(DbConnection connection, CancellationToken cancellationToken);

        /// <inheritdoc />
        public abstract Task<IReadOnlyList<SchemaObject>> GetTablesAsync(DbConnection connection, string database, CancellationToken cancellationToken);

        /// <inheritdoc />
        public abstract Task<IReadOnlyList<SchemaObject>> GetViewsAsync(DbConnection connection, string database, CancellationToken cancellationToken);

        /// <inheritdoc />
        public abstract Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(DbConnection connection, string database, string schema, string objectName, CancellationToken cancellationToken);

        /// <inheritdoc />
        public abstract void AttachMessageHandler(DbConnection connection, Action<string> onMessage);

        /// <inheritdoc />
        public abstract IEnumerable<string> GetErrorMessages(Exception exception);

        /// <inheritdoc />
        public abstract string? ExtractPassword(string connectionString);

        /// <inheritdoc />
        public abstract string StripPassword(string connectionString);

        /// <inheritdoc />
        public virtual async Task<string?> GetCurrentDatabaseAsync(DbConnection connection, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = CurrentDatabaseQuery;

            object? result = await command.ExecuteScalarAsync(cancellationToken);

            return result as string;
        }

        /// <inheritdoc />
        public virtual string DescribeServer(DbConnection connection)
        {
            return $"{connection.DataSource} ({DisplayName} {connection.ServerVersion})";
        }

        /// <summary>
        /// Runs a command whose first column is a name and returns the names in order.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="cancellationToken">Token to cancel reading.</param>
        /// <returns>The names.</returns>
        protected static async Task<IReadOnlyList<string>> ReadNamesAsync(DbCommand command, CancellationToken cancellationToken)
        {
            List<string> names = new();

            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken) == true)
            {
                if (reader.IsDBNull(0) == false)
                {
                    names.Add(reader.GetString(0));
                }
            }

            return names;
        }

        /// <summary>
        /// Runs a command whose first two columns are (schema, name) and returns the
        /// resulting objects. A null schema becomes an empty string (engines without a
        /// schema layer).
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="cancellationToken">Token to cancel reading.</param>
        /// <returns>The objects.</returns>
        protected static async Task<IReadOnlyList<SchemaObject>> ReadSchemaObjectsAsync(DbCommand command, CancellationToken cancellationToken)
        {
            List<SchemaObject> objects = new();

            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken) == true)
            {
                string schema = reader.IsDBNull(0) == true ? string.Empty : reader.GetString(0);

                objects.Add(new SchemaObject
                {
                    Schema = schema,
                    Name = reader.GetString(1)
                });
            }

            return objects;
        }

        /// <summary>
        /// Adds a parameter with a value to a command, returning the created parameter.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="name">The parameter name (including any engine prefix).</param>
        /// <param name="value">The parameter value.</param>
        protected static void AddParameter(DbCommand command, string name, object value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;

            command.Parameters.Add(parameter);
        }
    }
}
