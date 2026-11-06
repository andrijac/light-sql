using LightSQL.Models;

namespace LightSQL.Services.Database
{
    /// <summary>
    /// SQL dialect for PostgreSQL: double-quote (<c>"ident"</c>) quoting, <c>LIMIT n</c>
    /// and the <c>public</c> default schema.
    /// </summary>
    public sealed class PostgreSqlDialect : SqlDialectBase
    {
        /// <inheritdoc />
        public override DatabaseEngine Engine
        {
            get
            {
                return DatabaseEngine.PostgreSql;
            }
        }

        /// <inheritdoc />
        public override string? DefaultSchema
        {
            get
            {
                return "public";
            }
        }

        /// <inheritdoc />
        public override string QuoteIdentifier(string identifier)
        {
            return QuoteDelimited(identifier, '"', '"');
        }

        /// <inheritdoc />
        public override string BuildSelectTop(string? schema, string name, int rowCount)
        {
            return $"SELECT * FROM {QualifyName(schema, name)} LIMIT {rowCount}";
        }
    }
}
