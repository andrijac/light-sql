using LightSQL.Models;
using LightSQL.Services.Database;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests building MySQL connection strings from profiles.
    /// </summary>
    public class MySqlDatabaseProviderTests
    {
        private readonly MySqlDatabaseProvider provider = new();

        [Fact]
        public void FormMode_IncludesServerPortUserPasswordDatabase()
        {
            ConnectionProfile profile = new()
            {
                Engine = DatabaseEngine.MySql,
                Server = "localhost",
                Port = 3307,
                UserId = "root",
                Password = "pw",
                Database = "sales"
            };

            string connectionString = provider.BuildConnectionString(profile, null);

            Assert.Contains("Server=localhost", connectionString);
            Assert.Contains("3307", connectionString);
            Assert.Contains("root", connectionString);
            Assert.Contains("pw", connectionString);
            Assert.Contains("sales", connectionString);
        }

        [Fact]
        public void OverrideDatabase_SetsDatabase()
        {
            ConnectionProfile profile = new()
            {
                Engine = DatabaseEngine.MySql,
                Server = "localhost",
                UserId = "root",
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
                Engine = DatabaseEngine.MySql,
                Server = "localhost",
                UserId = "root",
                Password = "pw",
                Encrypt = false
            };

            string connectionString = provider.BuildConnectionString(profile, null);

            Assert.Contains("None", connectionString);
        }

        [Fact]
        public void AdministrativeDatabase_IsNull()
        {
            Assert.Null(provider.AdministrativeDatabase);
        }
    }
}
