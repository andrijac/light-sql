using LightSQL.Models;
using LightSQL.Services.Database;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests building SQL Server connection strings from profiles, for both authentication
    /// modes and the raw connection-string mode.
    /// </summary>
    public class SqlServerDatabaseProviderTests
    {
        private readonly SqlServerDatabaseProvider provider = new();

        [Fact]
        public void WindowsAuth_UsesIntegratedSecurity_AndNoPassword()
        {
            ConnectionProfile profile = new()
            {
                Server = "localhost",
                AuthenticationType = AuthenticationType.Windows,
                Database = "master"
            };

            string connectionString = provider.BuildConnectionString(profile, null);

            Assert.Contains("Integrated Security=True", connectionString);
            Assert.Contains("Data Source=localhost", connectionString);
            Assert.Contains("Initial Catalog=master", connectionString);
            Assert.DoesNotContain("Password", connectionString);
        }

        [Fact]
        public void SqlAuth_IncludesUserAndPassword()
        {
            ConnectionProfile profile = new()
            {
                Server = "localhost",
                AuthenticationType = AuthenticationType.SqlServer,
                UserId = "sa",
                Password = "p@ss"
            };

            string connectionString = provider.BuildConnectionString(profile, null);

            Assert.Contains("User ID=sa", connectionString);
            Assert.Contains("p@ss", connectionString);
        }

        [Fact]
        public void Port_IsAppendedToDataSource()
        {
            ConnectionProfile profile = new()
            {
                Server = "localhost",
                Port = 1433,
                AuthenticationType = AuthenticationType.Windows
            };

            string connectionString = provider.BuildConnectionString(profile, null);

            Assert.Contains("Data Source=localhost,1433", connectionString);
        }

        [Fact]
        public void OverrideDatabase_SetsInitialCatalog()
        {
            ConnectionProfile profile = new()
            {
                Server = "localhost",
                AuthenticationType = AuthenticationType.Windows,
                Database = "master"
            };

            string connectionString = provider.BuildConnectionString(profile, "AdventureWorks");

            Assert.Contains("Initial Catalog=AdventureWorks", connectionString);
        }

        [Fact]
        public void RawMode_InjectsPasswordWhenMissing()
        {
            ConnectionProfile profile = new()
            {
                UseRawConnectionString = true,
                RawConnectionString = "Server=localhost;User ID=sa;",
                Password = "injected"
            };

            string connectionString = provider.BuildConnectionString(profile, null);

            Assert.Contains("injected", connectionString);
        }
    }
}
