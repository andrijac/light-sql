using LightSQL.Models;
using LightSQL.Services.Database;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests building PostgreSQL connection strings from profiles.
    /// </summary>
    public class PostgreSqlDatabaseProviderTests
    {
        private readonly PostgreSqlDatabaseProvider provider = new();

        [Fact]
        public void FormMode_IncludesHostPortUserPasswordDatabase()
        {
            ConnectionProfile profile = new()
            {
                Engine = DatabaseEngine.PostgreSql,
                Server = "localhost",
                Port = 5433,
                UserId = "app",
                Password = "pw",
                Database = "sales"
            };

            string connectionString = provider.BuildConnectionString(profile, null);

            Assert.Contains("Host=localhost", connectionString);
            Assert.Contains("5433", connectionString);
            Assert.Contains("app", connectionString);
            Assert.Contains("pw", connectionString);
            Assert.Contains("sales", connectionString);
        }

        [Fact]
        public void NoDatabase_FallsBackToPostgres()
        {
            ConnectionProfile profile = new()
            {
                Engine = DatabaseEngine.PostgreSql,
                Server = "localhost",
                UserId = "app",
                Password = "pw"
            };

            string connectionString = provider.BuildConnectionString(profile, null);

            Assert.Contains("postgres", connectionString);
        }

        [Fact]
        public void OverrideDatabase_SetsDatabase()
        {
            ConnectionProfile profile = new()
            {
                Engine = DatabaseEngine.PostgreSql,
                Server = "localhost",
                UserId = "app",
                Password = "pw",
                Database = "sales"
            };

            string connectionString = provider.BuildConnectionString(profile, "metadb");

            Assert.Contains("metadb", connectionString);
        }

        [Fact]
        public void EncryptFalse_DisablesSsl()
        {
            ConnectionProfile profile = new()
            {
                Engine = DatabaseEngine.PostgreSql,
                Server = "localhost",
                UserId = "app",
                Password = "pw",
                Encrypt = false
            };

            string connectionString = provider.BuildConnectionString(profile, null);

            Assert.Contains("Disable", connectionString);
        }

        [Fact]
        public void AdministrativeDatabase_IsPostgres()
        {
            Assert.Equal("postgres", provider.AdministrativeDatabase);
        }
    }
}
