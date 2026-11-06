using LightSQL.Models;
using LightSQL.Services.Database;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests that the provider registry resolves one provider per engine and exposes them all.
    /// </summary>
    public class DatabaseProviderRegistryTests
    {
        private readonly DatabaseProviderRegistry registry = new();

        [Fact]
        public void Providers_ContainsAllThreeEngines()
        {
            Assert.Equal(3, registry.Providers.Count);
        }

        [Theory]
        [InlineData(DatabaseEngine.SqlServer, typeof(SqlServerDatabaseProvider))]
        [InlineData(DatabaseEngine.PostgreSql, typeof(PostgreSqlDatabaseProvider))]
        [InlineData(DatabaseEngine.MySql, typeof(MySqlDatabaseProvider))]
        public void Get_ReturnsMatchingProvider(DatabaseEngine engine, Type expectedType)
        {
            IDatabaseProvider provider = registry.Get(engine);

            Assert.Equal(engine, provider.Engine);
            Assert.IsType(expectedType, provider);
        }

        [Fact]
        public void Get_UnknownEngine_Throws()
        {
            Assert.Throws<NotSupportedException>(() => registry.Get((DatabaseEngine)999));
        }

        [Fact]
        public void ResolvedFromDependencyInjection_IsPopulated()
        {
            // Guards against a constructor-selection regression: the DI container would
            // otherwise pick a greedier constructor and build an empty registry.
            ServiceCollection services = new();
            services.AddSingleton<IDatabaseProviderRegistry, DatabaseProviderRegistry>();

            using ServiceProvider provider = services.BuildServiceProvider();
            IDatabaseProviderRegistry resolved = provider.GetRequiredService<IDatabaseProviderRegistry>();

            Assert.Equal(3, resolved.Providers.Count);
            Assert.IsType<SqlServerDatabaseProvider>(resolved.Get(DatabaseEngine.SqlServer));
        }
    }
}
