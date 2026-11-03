using System.IO;
using LightSQL.Models;
using LightSQL.Services;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests that connection profiles round-trip through the store and, critically,
    /// that no plaintext password is ever written to disk.
    /// </summary>
    public class ConnectionStoreTests : IDisposable
    {
        private readonly string filePath;

        /// <summary>
        /// Creates a store backed by a unique temporary file.
        /// </summary>
        public ConnectionStoreTests()
        {
            filePath = Path.Combine(Path.GetTempPath(), $"lightsql-conn-{Guid.NewGuid():N}.json");
        }

        /// <summary>
        /// Removes the temporary file.
        /// </summary>
        public void Dispose()
        {
            if (File.Exists(filePath) == true)
            {
                File.Delete(filePath);
            }
        }

        [Fact]
        public void Save_WithSavedPassword_EncryptsAndRoundTrips()
        {
            const string password = "MyPlainPassword123";

            ConnectionStore store = new(new FakeCredentialProtector(), filePath);

            ConnectionProfile profile = new()
            {
                Name = "Local",
                Server = "localhost",
                AuthenticationType = AuthenticationType.SqlServer,
                UserId = "sa",
                Password = password,
                SavePassword = true
            };

            store.Save(profile);

            string fileContents = File.ReadAllText(filePath);
            Assert.DoesNotContain(password, fileContents);

            // A new store instance should decrypt the password back into memory.
            ConnectionStore reloaded = new(new FakeCredentialProtector(), filePath);
            ConnectionProfile loaded = Assert.Single(reloaded.GetAll());

            Assert.Equal("Local", loaded.Name);
            Assert.Equal(password, loaded.Password);
        }

        [Fact]
        public void Save_WithoutSavingPassword_DoesNotPersistPassword()
        {
            ConnectionStore store = new(new FakeCredentialProtector(), filePath);

            ConnectionProfile profile = new()
            {
                Name = "NoSave",
                Server = "localhost",
                AuthenticationType = AuthenticationType.SqlServer,
                UserId = "sa",
                Password = "ShouldNotPersist",
                SavePassword = false
            };

            store.Save(profile);

            string fileContents = File.ReadAllText(filePath);
            Assert.DoesNotContain("ShouldNotPersist", fileContents);

            ConnectionStore reloaded = new(new FakeCredentialProtector(), filePath);
            ConnectionProfile loaded = Assert.Single(reloaded.GetAll());

            Assert.Null(loaded.Password);
        }

        [Fact]
        public void Save_RawConnectionString_StripsEmbeddedPassword()
        {
            ConnectionStore store = new(new FakeCredentialProtector(), filePath);

            ConnectionProfile profile = new()
            {
                Name = "Raw",
                UseRawConnectionString = true,
                RawConnectionString = "Server=localhost;User ID=sa;Password=EmbeddedSecret;",
                SavePassword = false
            };

            store.Save(profile);

            string fileContents = File.ReadAllText(filePath);
            Assert.DoesNotContain("EmbeddedSecret", fileContents);
        }

        [Fact]
        public void Delete_RemovesProfile()
        {
            ConnectionStore store = new(new FakeCredentialProtector(), filePath);
            store.Save(new ConnectionProfile { Name = "ToDelete", Server = "localhost" });

            store.Delete("ToDelete");

            Assert.Empty(store.GetAll());
        }

        [Fact]
        public void Save_ExistingName_UpdatesInPlace()
        {
            ConnectionStore store = new(new FakeCredentialProtector(), filePath);
            store.Save(new ConnectionProfile { Name = "Dup", Server = "server-a" });
            store.Save(new ConnectionProfile { Name = "Dup", Server = "server-b" });

            ConnectionProfile loaded = Assert.Single(store.GetAll());
            Assert.Equal("server-b", loaded.Server);
        }
    }
}
