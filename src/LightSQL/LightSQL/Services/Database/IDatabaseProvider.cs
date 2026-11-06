using System.Data.Common;
using LightSQL.Models;

namespace LightSQL.Services.Database
{
    /// <summary>
    /// Everything that differs between database engines: creating connections, building
    /// connection strings, reading metadata, formatting errors and server-level details.
    /// One implementation per engine. The engine-agnostic <see cref="ISqlConnectionService"/>
    /// owns connection lifetime (open / close / transactions) over <see cref="DbConnection"/>
    /// and delegates every engine-specific decision here.
    /// <para>
    /// Adding a new engine means implementing this interface plus an
    /// <see cref="ISqlDialect"/> and registering it in <c>DatabaseProviderRegistry</c>;
    /// no other code changes.
    /// </para>
    /// </summary>
    public interface IDatabaseProvider
    {
        /// <summary>
        /// The engine this provider serves.
        /// </summary>
        DatabaseEngine Engine { get; }

        /// <summary>
        /// Human-readable engine name for the UI, e.g. "Microsoft SQL Server".
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// The SQL-text dialect for this engine (quoting, SELECT TOP/LIMIT, CREATE/DROP).
        /// </summary>
        ISqlDialect Dialect { get; }

        /// <summary>
        /// Whether the engine supports Windows / integrated authentication (SQL Server
        /// only). When false the connection dialog always requires a username and password.
        /// </summary>
        bool SupportsWindowsAuthentication { get; }

        /// <summary>
        /// The engine's default TCP port, shown as a hint in the connection dialog.
        /// </summary>
        int DefaultPort { get; }

        /// <summary>
        /// The database a server-level operation (listing or creating databases) must run
        /// in, or null when any connection will do. For example "master" (SQL Server) or
        /// "postgres" (PostgreSQL); null for MySQL.
        /// </summary>
        string? AdministrativeDatabase { get; }

        /// <summary>
        /// Creates (but does not open) a connection for this engine.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <returns>A new, closed connection.</returns>
        DbConnection CreateConnection(string connectionString);

        /// <summary>
        /// Builds a connection string from a profile, optionally overriding the target
        /// database (used to point metadata lookups at a specific database).
        /// </summary>
        /// <param name="profile">The connection definition.</param>
        /// <param name="overrideDatabase">The database to target, or null for the profile's own.</param>
        /// <returns>The connection string.</returns>
        string BuildConnectionString(ConnectionProfile profile, string? overrideDatabase);

        /// <summary>
        /// Reads the database the connection is currently in (DB_NAME() / current_database()
        /// / DATABASE()).
        /// </summary>
        /// <param name="connection">An open connection.</param>
        /// <param name="cancellationToken">Token to cancel the lookup.</param>
        /// <returns>The current database name, or null.</returns>
        Task<string?> GetCurrentDatabaseAsync(DbConnection connection, CancellationToken cancellationToken);

        /// <summary>
        /// A short description of the reached server, e.g. "localhost (PostgreSQL 16.1)".
        /// </summary>
        /// <param name="connection">An open connection.</param>
        /// <returns>The description.</returns>
        string DescribeServer(DbConnection connection);

        /// <summary>
        /// Lists the databases (schemas, for MySQL) visible on the server.
        /// </summary>
        /// <param name="connection">An open connection, targeting <see cref="AdministrativeDatabase"/> where required.</param>
        /// <param name="cancellationToken">Token to cancel the lookup.</param>
        /// <returns>Database names, ordered alphabetically.</returns>
        Task<IReadOnlyList<string>> GetDatabasesAsync(DbConnection connection, CancellationToken cancellationToken);

        /// <summary>
        /// Lists the base tables of a database.
        /// </summary>
        /// <param name="connection">An open connection targeting the database.</param>
        /// <param name="database">The database name (used where the engine filters by it).</param>
        /// <param name="cancellationToken">Token to cancel the lookup.</param>
        /// <returns>The tables, ordered by schema then name.</returns>
        Task<IReadOnlyList<SchemaObject>> GetTablesAsync(DbConnection connection, string database, CancellationToken cancellationToken);

        /// <summary>
        /// Lists the views of a database.
        /// </summary>
        /// <param name="connection">An open connection targeting the database.</param>
        /// <param name="database">The database name (used where the engine filters by it).</param>
        /// <param name="cancellationToken">Token to cancel the lookup.</param>
        /// <returns>The views, ordered by schema then name.</returns>
        Task<IReadOnlyList<SchemaObject>> GetViewsAsync(DbConnection connection, string database, CancellationToken cancellationToken);

        /// <summary>
        /// Lists the columns of a table or view.
        /// </summary>
        /// <param name="connection">An open connection targeting the database.</param>
        /// <param name="database">The owning database (used by engines where schema == database).</param>
        /// <param name="schema">The object's schema (ignored by engines without schemas).</param>
        /// <param name="objectName">The table or view name.</param>
        /// <param name="cancellationToken">Token to cancel the lookup.</param>
        /// <returns>The columns, in ordinal order.</returns>
        Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(DbConnection connection, string database, string schema, string objectName, CancellationToken cancellationToken);

        /// <summary>
        /// Subscribes to the connection's informational messages (PRINT, notices, warnings),
        /// forwarding each one to <paramref name="onMessage"/>. The subscription lives as long
        /// as the (short-lived) connection.
        /// </summary>
        /// <param name="connection">An open connection created by this provider.</param>
        /// <param name="onMessage">Called once per message line.</param>
        void AttachMessageHandler(DbConnection connection, Action<string> onMessage);

        /// <summary>
        /// Extracts human-readable error lines from a provider-specific exception. Returns
        /// an empty sequence when the exception is not one this provider recognises, so the
        /// caller can fall back to <see cref="Exception.Message"/>.
        /// </summary>
        /// <param name="exception">The exception thrown during execution.</param>
        /// <returns>The error lines, or empty.</returns>
        IEnumerable<string> GetErrorMessages(Exception exception);

        /// <summary>
        /// Returns the password embedded in a raw connection string, or null when there is
        /// none or it cannot be parsed. Used to move a password into encrypted storage.
        /// </summary>
        /// <param name="connectionString">The raw connection string.</param>
        /// <returns>The password, or null.</returns>
        string? ExtractPassword(string connectionString);

        /// <summary>
        /// Removes any password from a raw connection string so it can be persisted safely.
        /// </summary>
        /// <param name="connectionString">The raw connection string.</param>
        /// <returns>The connection string without a password.</returns>
        string StripPassword(string connectionString);
    }
}
