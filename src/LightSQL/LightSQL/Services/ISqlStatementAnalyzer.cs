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
    }
}
