using LightSQL.Models;

namespace LightSQL.Services.Database
{
    /// <summary>
    /// T-SQL dialect for Microsoft SQL Server: <c>[bracket]</c> quoting, <c>SELECT TOP n</c>
    /// and the <c>dbo</c> default schema.
    /// </summary>
    public sealed class SqlServerDialect : SqlDialectBase
    {
        /// <inheritdoc />
        public override DatabaseEngine Engine
        {
            get
            {
                return DatabaseEngine.SqlServer;
            }
        }

        /// <inheritdoc />
        public override string? DefaultSchema
        {
            get
            {
                return "dbo";
            }
        }

        /// <inheritdoc />
        public override string QuoteIdentifier(string identifier)
        {
            return QuoteDelimited(identifier, '[', ']');
        }

        /// <inheritdoc />
        public override string BuildSelectTop(string? schema, string name, int rowCount)
        {
            return $"SELECT TOP {rowCount} * FROM {QualifyName(schema, name)}";
        }
    }
}
