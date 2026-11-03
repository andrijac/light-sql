using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// Manages the active SQL Server connection and runs queries and metadata
    /// lookups against it. All database work is asynchronous so the UI stays
    /// responsive.
    /// </summary>
    public interface ISqlConnectionService
    {
        /// <summary>
        /// Whether a connection has been established.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// The connected server / data-source name, or null when disconnected.
        /// </summary>
        string? ServerName { get; }

        /// <summary>
        /// The database used for query execution, or null when disconnected.
        /// </summary>
        string? CurrentDatabase { get; }

        /// <summary>
        /// Opens and validates a connection described by the given profile.
        /// </summary>
        /// <param name="profile">The connection to open.</param>
        /// <param name="cancellationToken">Token to cancel the attempt.</param>
        /// <returns>A short description of the connected server.</returns>
        Task<string> ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken);

        /// <summary>
        /// Opens and immediately closes a connection to validate a profile, without
        /// changing the active connection state.
        /// </summary>
        /// <param name="profile">The connection to test.</param>
        /// <param name="cancellationToken">Token to cancel the attempt.</param>
        /// <returns>A short description of the server that was reached.</returns>
        Task<string> TestConnectionAsync(ConnectionProfile profile, CancellationToken cancellationToken);

        /// <summary>
        /// Clears the active connection state.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Sets the database used for subsequent query execution.
        /// </summary>
        /// <param name="database">The database name to make active.</param>
        void SetCurrentDatabase(string database);

        /// <summary>
        /// Executes a SQL batch and returns its result sets, messages and timing.
        /// </summary>
        /// <param name="sql">The SQL batch to run.</param>
        /// <param name="cancellationToken">Token to cancel execution.</param>
        /// <returns>The execution result.</returns>
        Task<QueryExecutionResult> ExecuteBatchAsync(string sql, CancellationToken cancellationToken);

        /// <summary>
        /// Lists the online databases on the connected server.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the lookup.</param>
        /// <returns>Database names, ordered alphabetically.</returns>
        Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Lists the user tables in a database.
        /// </summary>
        /// <param name="database">The database to inspect.</param>
        /// <param name="cancellationToken">Token to cancel the lookup.</param>
        /// <returns>The tables, ordered by schema then name.</returns>
        Task<IReadOnlyList<SchemaObject>> GetTablesAsync(string database, CancellationToken cancellationToken);

        /// <summary>
        /// Lists the views in a database.
        /// </summary>
        /// <param name="database">The database to inspect.</param>
        /// <param name="cancellationToken">Token to cancel the lookup.</param>
        /// <returns>The views, ordered by schema then name.</returns>
        Task<IReadOnlyList<SchemaObject>> GetViewsAsync(string database, CancellationToken cancellationToken);

        /// <summary>
        /// Lists the columns of a table or view.
        /// </summary>
        /// <param name="database">The database that owns the object.</param>
        /// <param name="schema">The object's schema.</param>
        /// <param name="objectName">The object name.</param>
        /// <param name="cancellationToken">Token to cancel the lookup.</param>
        /// <returns>The columns, in ordinal order.</returns>
        Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string database, string schema, string objectName, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a new database on the connected server.
        /// </summary>
        /// <param name="databaseName">The name of the database to create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task that completes when the database has been created.</returns>
        Task CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken);
    }
}
