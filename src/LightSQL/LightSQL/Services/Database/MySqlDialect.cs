using LightSQL.Models;

namespace LightSQL.Services.Database
{
    /// <summary>
    /// SQL dialect for MySQL / MariaDB: backtick (<c>`ident`</c>) quoting and <c>LIMIT n</c>.
    /// MySQL has no schema layer (a "schema" is a database), so there is no default
    /// schema and object references are the bare, quoted object name run against the
    /// connection's current database.
    /// </summary>
    public sealed class MySqlDialect : SqlDialectBase
    {
        /// <inheritdoc />
        public override DatabaseEngine Engine
        {
            get
            {
                return DatabaseEngine.MySql;
            }
        }

        /// <inheritdoc />
        public override string? DefaultSchema
        {
            get
            {
                return null;
            }
        }

        /// <inheritdoc />
        public override string QuoteIdentifier(string identifier)
        {
            return QuoteDelimited(identifier, '`', '`');
        }

        /// <summary>
        /// Qualifies an object. MySQL has no schema layer, so the schema is ignored and
        /// only the object name is quoted; the connection already targets the database.
        /// </summary>
        /// <param name="schema">Ignored for MySQL.</param>
        /// <param name="name">The object name.</param>
        /// <returns>The quoted object name.</returns>
        public override string QualifyName(string? schema, string name)
        {
            return QuoteIdentifier(name);
        }

        /// <inheritdoc />
        public override string BuildSelectTop(string? schema, string name, int rowCount)
        {
            return $"SELECT * FROM {QualifyName(schema, name)} LIMIT {rowCount}";
        }
    }
}
