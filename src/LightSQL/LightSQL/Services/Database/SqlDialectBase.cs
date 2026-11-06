using LightSQL.Models;

namespace LightSQL.Services.Database
{
    /// <summary>
    /// Shared implementation of <see cref="ISqlDialect"/>. Concrete dialects supply the
    /// quote characters and the "top N" shape; everything else (qualification, CREATE /
    /// DROP) is derived from those so each engine needs only a few lines.
    /// </summary>
    public abstract class SqlDialectBase : ISqlDialect
    {
        /// <inheritdoc />
        public abstract DatabaseEngine Engine { get; }

        /// <inheritdoc />
        public abstract string? DefaultSchema { get; }

        /// <inheritdoc />
        public abstract string QuoteIdentifier(string identifier);

        /// <inheritdoc />
        public abstract string BuildSelectTop(string? schema, string name, int rowCount);

        /// <inheritdoc />
        public virtual string QualifyName(string? schema, string name)
        {
            if (string.IsNullOrWhiteSpace(schema) == true)
            {
                return QuoteIdentifier(name);
            }

            return $"{QuoteIdentifier(schema)}.{QuoteIdentifier(name)}";
        }

        /// <inheritdoc />
        public virtual string BuildCreateDatabase(string databaseName)
        {
            return $"CREATE DATABASE {QuoteIdentifier(databaseName)}";
        }

        /// <inheritdoc />
        public virtual string BuildDropTable(string? schema, string name)
        {
            return $"DROP TABLE {QualifyName(schema, name)}";
        }

        /// <inheritdoc />
        public virtual string BuildDropView(string? schema, string name)
        {
            return $"DROP VIEW {QualifyName(schema, name)}";
        }

        /// <inheritdoc />
        public virtual string BuildDropDatabase(string databaseName)
        {
            return $"DROP DATABASE {QuoteIdentifier(databaseName)}";
        }

        /// <summary>
        /// Quotes an identifier by wrapping it in the given delimiters and escaping any
        /// closing delimiter inside it by doubling it (the convention all three engines use).
        /// </summary>
        /// <param name="identifier">The raw identifier.</param>
        /// <param name="open">The opening delimiter.</param>
        /// <param name="close">The closing delimiter.</param>
        /// <returns>The quoted, escaped identifier.</returns>
        protected static string QuoteDelimited(string identifier, char open, char close)
        {
            string escaped = identifier.Replace(close.ToString(), $"{close}{close}");

            return $"{open}{escaped}{close}";
        }
    }
}
