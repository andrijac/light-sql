namespace LightSQL.Services
{
    /// <summary>
    /// Inspects a SQL batch for risky statements, such as an UPDATE or DELETE with
    /// no WHERE clause, so the user can be warned before executing.
    /// </summary>
    public interface ISqlStatementAnalyzer
    {
        /// <summary>
        /// Determines whether the batch contains at least one UPDATE or DELETE
        /// statement that has no WHERE clause of its own. Matches inside string
        /// literals, comments, bracketed/quoted identifiers and subqueries are
        /// ignored.
        /// </summary>
        /// <param name="sql">The SQL batch text to analyze.</param>
        /// <returns>True if an unguarded UPDATE or DELETE is found.</returns>
        bool HasUnguardedUpdateOrDelete(string sql);

        /// <summary>
        /// Finds the first part of a batch that could change data or server state
        /// (INSERT, UPDATE, DDL, EXEC, transaction control, a bare procedure call,
        /// ...), for the read-only mode of the MCP server. Matches inside string
        /// literals, comments and bracketed/quoted identifiers are ignored. The check
        /// is biased towards blocking: a batch is only considered read-only when it
        /// consists of queries (SELECT / WITH), variables and control flow.
        /// </summary>
        /// <param name="sql">The SQL batch text to analyze.</param>
        /// <returns>The offending keyword or a short description, or null when the batch only reads.</returns>
        string? FindWriteOperation(string sql);
    }
}
