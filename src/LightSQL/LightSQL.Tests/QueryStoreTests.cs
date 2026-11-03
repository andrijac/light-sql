using System.IO;
using LightSQL.Models;
using LightSQL.Services;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests saving, reading and listing named queries, plus name validation.
    /// </summary>
    public class QueryStoreTests : IDisposable
    {
        private readonly string optionsPath;
        private readonly string queriesFolder;
        private readonly QueryStore store;

        /// <summary>
        /// Creates a store that writes into a unique temporary queries folder.
        /// </summary>
        public QueryStoreTests()
        {
            string id = Guid.NewGuid().ToString("N");
            optionsPath = Path.Combine(Path.GetTempPath(), $"lightsql-opts-{id}.json");
            queriesFolder = Path.Combine(Path.GetTempPath(), $"lightsql-queries-{id}");

            OptionsService options = new(optionsPath);
            options.Save(new AppOptions { QueriesFolder = queriesFolder });

            store = new QueryStore(options);
        }

        /// <summary>
        /// Removes the temporary files and folder.
        /// </summary>
        public void Dispose()
        {
            if (File.Exists(optionsPath) == true)
            {
                File.Delete(optionsPath);
            }

            if (Directory.Exists(queriesFolder) == true)
            {
                Directory.Delete(queriesFolder, true);
            }
        }

        [Fact]
        public void Save_CreatesFile_AndIsListed()
        {
            store.Save("my-query", "SELECT 1");

            string expectedPath = Path.Combine(queriesFolder, "my-query.sql");
            Assert.True(File.Exists(expectedPath));
            Assert.Equal("SELECT 1", File.ReadAllText(expectedPath));

            SavedQuery listed = Assert.Single(store.GetAll());
            Assert.Equal("my-query", listed.Name);
        }

        [Fact]
        public void ReadContent_ReturnsSavedText()
        {
            store.Save("round-trip", "SELECT TOP 10 * FROM sys.objects");

            string content = store.ReadContent("round-trip");

            Assert.Equal("SELECT TOP 10 * FROM sys.objects", content);
        }

        [Fact]
        public void Save_InvalidName_Throws()
        {
            Assert.Throws<ArgumentException>(() => store.Save("bad/name", "SELECT 1"));
            Assert.Throws<ArgumentException>(() => store.Save("   ", "SELECT 1"));
        }

        [Fact]
        public void IsValidName_RejectsInvalidCharacters()
        {
            Assert.True(store.IsValidName("good_name"));
            Assert.False(store.IsValidName(string.Empty));
            Assert.False(store.IsValidName("has/slash"));
            Assert.False(store.IsValidName("has:colon"));
        }

        [Fact]
        public void Delete_RemovesSavedQuery()
        {
            store.Save("to-delete", "SELECT 1");
            store.Delete("to-delete");

            Assert.Empty(store.GetAll());
        }
    }
}
