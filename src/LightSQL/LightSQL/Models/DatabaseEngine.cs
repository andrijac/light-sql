namespace LightSQL.Models
{
    /// <summary>
    /// The database engine a connection targets. Each value maps to one
    /// <c>IDatabaseProvider</c> / <c>ISqlDialect</c> implementation.
    /// Values start at 1 so an unset (default) value is never a valid choice.
    /// </summary>
    public enum DatabaseEngine
    {
        /// <summary>
        /// Microsoft SQL Server (Microsoft.Data.SqlClient).
        /// </summary>
        SqlServer = 1,

        /// <summary>
        /// PostgreSQL (Npgsql).
        /// </summary>
        PostgreSql = 2,

        /// <summary>
        /// MySQL / MariaDB (MySqlConnector).
        /// </summary>
        MySql = 3
    }
}
