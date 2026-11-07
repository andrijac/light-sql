using System.IO;
using LightSQL.Models;
using LightSQL.Services;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the JSON-backed <see cref="MetadataStore"/>: round-tripping entries, keying
    /// by connection + database, and resilience to a missing file.
    /// </summary>
    public sealed class MetadataStoreTests : IDisposable
    {
        private readonly string filePath = Path.Combine(Path.GetTempPath(), $"lightsql-metadata-{Guid.NewGuid():N}.json");

        [Fact]
        public void Get_ReturnsNull_WhenNothingSaved()
        {
            MetadataStore store = new(filePath);

            Assert.Null(store.Get("conn1", "Sales"));
            Assert.Empty(store.GetAll());
        }

        [Fact]
        public void Save_ThenReload_RoundTripsTheEntry()
        {
            MetadataStore store = new(filePath);
            store.Save(new ConnectionMetadata { ConnectionName = "conn1", Database = "Sales", Json = "{\"ok\":true}" });

            MetadataStore reloaded = new(filePath);
            ConnectionMetadata? entry = reloaded.Get("conn1", "Sales");

            Assert.NotNull(entry);
            Assert.Equal("{\"ok\":true}", entry!.Json);
        }

        [Fact]
        public void Save_ReplacesExistingEntry_ForSameConnectionAndDatabase()
        {
            MetadataStore store = new(filePath);
            store.Save(new ConnectionMetadata { ConnectionName = "conn1", Database = "Sales", Json = "old" });
            store.Save(new ConnectionMetadata { ConnectionName = "conn1", Database = "Sales", Json = "new" });

            Assert.Equal("new", store.Get("conn1", "Sales")!.Json);
            Assert.Single(store.GetAll());
        }

        [Fact]
        public void Get_MatchesConnectionAndDatabase_CaseInsensitively()
        {
            MetadataStore store = new(filePath);
            store.Save(new ConnectionMetadata { ConnectionName = "Conn1", Database = "Sales", Json = "x" });

            Assert.NotNull(store.Get("conn1", "sales"));
        }

        [Fact]
        public void DifferentDatabases_AreSeparateEntries()
        {
            MetadataStore store = new(filePath);
            store.Save(new ConnectionMetadata { ConnectionName = "conn1", Database = "Sales", Json = "a" });
            store.Save(new ConnectionMetadata { ConnectionName = "conn1", Database = "HR", Json = "b" });

            Assert.Equal(2, store.GetAll().Count);
            Assert.Equal("a", store.Get("conn1", "Sales")!.Json);
            Assert.Equal("b", store.Get("conn1", "HR")!.Json);
        }

        [Fact]
        public void Delete_RemovesTheEntry()
        {
            MetadataStore store = new(filePath);
            store.Save(new ConnectionMetadata { ConnectionName = "conn1", Database = "Sales", Json = "x" });

            store.Delete("conn1", "Sales");

            Assert.Null(store.Get("conn1", "Sales"));
            Assert.Empty(store.GetAll());
        }

        /// <summary>
        /// Deletes the temporary metadata file.
        /// </summary>
        public void Dispose()
        {
            if (File.Exists(filePath) == true)
            {
                File.Delete(filePath);
            }
        }
    }
}
