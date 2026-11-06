using LightSQL.Models;

namespace LightSQL.Services.Database
{
    /// <summary>
    /// Pure, side-effect-free generation of engine-specific SQL text: identifier
    /// quoting, "top N" queries, and CREATE / DROP statements. Kept separate from
    /// <see cref="IDatabaseProvider"/> (which does I/O) so these rules are trivially
    /// unit-testable and can be shared by both the provider and the UI (Object
    /// Explorer) without exposing connection concerns.
    /// </summary>
    public interface ISqlDialect
    {
        /// <summary>
        /// The engine this dialect targets.
        /// </summary>
        DatabaseEngine Engine { get; }

        /// <summary>
        /// The schema used when a caller names none, or null for engines without a
        /// schema layer (MySQL, where a "schema" is a database). For example "dbo"
        /// (SQL Server) or "public" (PostgreSQL).
        /// </summary>
        string? DefaultSchema { get; }

        /// <summary>
        /// Quotes a single identifier so reserved words and special characters are safe,
        /// e.g. "Order Details" becomes "[Order Details]", "\"Order Details\"" or
        /// "`Order Details`" depending on the engine.
        /// </summary>
        /// <param name="identifier">The raw identifier.</param>
        /// <returns>The quoted identifier.</returns>
        string QuoteIdentifier(string identifier);

        /// <summary>
        /// Builds a quoted, optionally schema-qualified object reference. When the
        /// engine has no schema layer, the schema is ignored and only the name is quoted.
        /// </summary>
        /// <param name="schema">The schema name, or null/empty for none.</param>
        /// <param name="name">The object name.</param>
        /// <returns>The qualified, quoted reference.</returns>
        string QualifyName(string? schema, string name);

        /// <summary>
        /// Builds a query that returns at most <paramref name="rowCount"/> rows of a
        /// table or view (T-SQL <c>TOP</c> vs <c>LIMIT</c>).
        /// </summary>
        /// <param name="schema">The object's schema, or null/empty for none.</param>
        /// <param name="name">The object name.</param>
        /// <param name="rowCount">The maximum number of rows.</param>
        /// <returns>The query text.</returns>
        string BuildSelectTop(string? schema, string name, int rowCount);

        /// <summary>
        /// Builds a statement that creates a database.
        /// </summary>
        /// <param name="databaseName">The database name.</param>
        /// <returns>The CREATE DATABASE statement.</returns>
        string BuildCreateDatabase(string databaseName);

        /// <summary>
        /// Builds a statement that drops a table.
        /// </summary>
        /// <param name="schema">The table's schema, or null/empty for none.</param>
        /// <param name="name">The table name.</param>
        /// <returns>The DROP TABLE statement.</returns>
        string BuildDropTable(string? schema, string name);

        /// <summary>
        /// Builds a statement that drops a view.
        /// </summary>
        /// <param name="schema">The view's schema, or null/empty for none.</param>
        /// <param name="name">The view name.</param>
        /// <returns>The DROP VIEW statement.</returns>
        string BuildDropView(string? schema, string name);

        /// <summary>
        /// Builds a statement that drops a database.
        /// </summary>
        /// <param name="databaseName">The database name.</param>
        /// <returns>The DROP DATABASE statement.</returns>
        string BuildDropDatabase(string databaseName);
    }
}
