using LightSQL.Models;
using LightSQL.Services;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests building connection strings from profiles for both authentication modes
    /// and the raw connection-string mode.
    /// </summary>
    public class ConnectionStringFactoryTests
    {
        [Fact]
        public void WindowsAuth_UsesIntegratedSecurity_AndNoPassword()
        {
            ConnectionProfile profile = new()
            {
                Server = "localhost",
                AuthenticationType = AuthenticationType.Windows,
                Database = "master"
            };

            string connectionString = ConnectionStringFactory.Build(profile);

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

            string connectionString = ConnectionStringFactory.Build(profile);

            Assert.Contains("User ID=sa", connectionString);
            Assert.Contains("p@ss", connectionString);
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

            string connectionString = ConnectionStringFactory.Build(profile, "AdventureWorks");

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

            string connectionString = ConnectionStringFactory.Build(profile);

            Assert.Contains("injected", connectionString);
        }
    }
}
