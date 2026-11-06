namespace LightSQL.Models
{
    /// <summary>
    /// What an MCP server lets AI clients do with the database. Values start at 1
    /// so an unset (default) value is never mistaken for a real mode.
    /// </summary>
    public enum McpAccessMode
    {
        /// <summary>
        /// The default: only queries that read are allowed (others are rejected),
        /// and every batch runs in a transaction that is rolled back.
        /// </summary>
        ReadOnly = 1,

        /// <summary>
        /// Chosen by the user: any T-SQL may run, including data and schema changes.
        /// </summary>
        ReadWrite = 2
    }
}
