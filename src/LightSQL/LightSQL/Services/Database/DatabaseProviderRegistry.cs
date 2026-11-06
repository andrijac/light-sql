using LightSQL.Models;

namespace LightSQL.Services.Database
{
    /// <summary>
    /// Default <see cref="IDatabaseProviderRegistry"/>. Holds one provider per supported
    /// engine. Adding an engine means adding its provider to this registry (and nothing else).
    /// </summary>
    public sealed class DatabaseProviderRegistry : IDatabaseProviderRegistry
    {
        private readonly IReadOnlyList<IDatabaseProvider> providers;

        /// <summary>
        /// Creates the registry with the built-in providers (SQL Server, PostgreSQL, MySQL).
        /// This is the single place that lists the supported engines; adding one means
        /// adding its provider here.
        /// <para>
        /// Deliberately the only constructor: Microsoft's DI container picks the greediest
        /// resolvable constructor, so an <c>IEnumerable&lt;IDatabaseProvider&gt;</c> overload
        /// would be chosen and resolved to an empty set (no providers registered), leaving
        /// the registry empty.
        /// </para>
        /// </summary>
        public DatabaseProviderRegistry()
        {
            providers = new IDatabaseProvider[]
            {
                new SqlServerDatabaseProvider(),
                new PostgreSqlDatabaseProvider(),
                new MySqlDatabaseProvider()
            };
        }

        /// <inheritdoc />
        public IReadOnlyList<IDatabaseProvider> Providers
        {
            get
            {
                return providers;
            }
        }

        /// <inheritdoc />
        public IDatabaseProvider Get(DatabaseEngine engine)
        {
            foreach (IDatabaseProvider provider in providers)
            {
                if (provider.Engine == engine)
                {
                    return provider;
                }
            }

            throw new NotSupportedException($"No database provider is registered for engine '{engine}'.");
        }
    }
}
