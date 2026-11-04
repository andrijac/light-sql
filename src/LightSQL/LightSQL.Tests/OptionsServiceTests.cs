using System.IO;
using LightSQL.Models;
using LightSQL.Services;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests loading, defaults and persistence of application options.
    /// </summary>
    public class OptionsServiceTests : IDisposable
    {
        private readonly string filePath;

        /// <summary>
        /// Creates a service backed by a unique temporary file.
        /// </summary>
        public OptionsServiceTests()
        {
            filePath = Path.Combine(Path.GetTempPath(), $"lightsql-options-{Guid.NewGuid():N}.json");
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
        public void NewService_HasSafeDefaults()
        {
            OptionsService service = new(filePath);

            Assert.True(service.Current.WarnOnUpdateDeleteWithoutWhere);
            Assert.True(service.Current.ShowLineNumbers);
            Assert.Equal(13d, service.Current.EditorFontSize);
        }

        [Fact]
        public void Save_ThenReload_PersistsValues()
        {
            OptionsService service = new(filePath);

            AppOptions options = new()
            {
                WarnOnUpdateDeleteWithoutWhere = false,
                ShowLineNumbers = false,
                EditorFontSize = 16d
            };

            service.Save(options);

            OptionsService reloaded = new(filePath);

            Assert.False(reloaded.Current.WarnOnUpdateDeleteWithoutWhere);
            Assert.False(reloaded.Current.ShowLineNumbers);
            Assert.Equal(16d, reloaded.Current.EditorFontSize);
        }

        [Fact]
        public void Load_OldFileWithQueriesFolder_StillLoads()
        {
            // Options files written before saved queries were removed contain a
            // "QueriesFolder" value; it must be ignored, not break loading.
            File.WriteAllText(filePath, "{ \"WarnOnUpdateDeleteWithoutWhere\": false, \"QueriesFolder\": \"C:\\\\Temp\", \"EditorFontSize\": 15 }");

            OptionsService service = new(filePath);

            Assert.False(service.Current.WarnOnUpdateDeleteWithoutWhere);
            Assert.Equal(15d, service.Current.EditorFontSize);
        }
    }
}
