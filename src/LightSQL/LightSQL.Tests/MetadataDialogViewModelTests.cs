using LightSQL.Models;
using LightSQL.ViewModels;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the Database Metadata dialog view model: offering connections, prefilling
    /// saved metadata, validating JSON and saving (or clearing) an entry.
    /// </summary>
    public class MetadataDialogViewModelTests
    {
        private readonly FakeMetadataStore metadataStore = new();
        private readonly FakeConnectionStore connectionStore = new(
            new ConnectionProfile { Name = "conn1" },
            new ConnectionProfile { Name = "conn2" });

        [Fact]
        public void Constructor_OffersTheSavedConnectionNames()
        {
            MetadataDialogViewModel viewModel = CreateViewModel();

            Assert.Equal(new[] { "conn1", "conn2" }, viewModel.Connections);
        }

        [Fact]
        public void Initialize_PrefillsSavedMetadata()
        {
            metadataStore.Save(new ConnectionMetadata { ConnectionName = "conn1", Database = "Sales", Json = "{\"ok\":true}" });
            MetadataDialogViewModel viewModel = CreateViewModel();

            viewModel.Initialize("conn1", "Sales");

            Assert.Equal("conn1", viewModel.ConnectionName);
            Assert.Equal("Sales", viewModel.Database);
            Assert.Equal("{\"ok\":true}", viewModel.Json);
        }

        [Fact]
        public void TrySave_WithValidJson_SavesTheEntry()
        {
            MetadataDialogViewModel viewModel = CreateViewModel();
            viewModel.Initialize("conn1", "Sales");
            viewModel.Json = "{\"tables\":[]}";

            bool saved = viewModel.TrySave(out string? error);

            Assert.True(saved);
            Assert.Null(error);
            Assert.True(viewModel.Saved);
            Assert.Equal("{\"tables\":[]}", metadataStore.Get("conn1", "Sales")!.Json);
        }

        [Fact]
        public void TrySave_WithInvalidJson_Fails_WithoutSaving()
        {
            MetadataDialogViewModel viewModel = CreateViewModel();
            viewModel.Initialize("conn1", "Sales");
            viewModel.Json = "{not json";

            bool saved = viewModel.TrySave(out string? error);

            Assert.False(saved);
            Assert.NotNull(error);
            Assert.False(viewModel.Saved);
            Assert.Null(metadataStore.Get("conn1", "Sales"));
        }

        [Fact]
        public void TrySave_WithNoConnection_Fails()
        {
            MetadataDialogViewModel viewModel = CreateViewModel();
            viewModel.Json = "{}";

            bool saved = viewModel.TrySave(out string? error);

            Assert.False(saved);
            Assert.NotNull(error);
        }

        [Fact]
        public void TrySave_WithEmptyJson_RemovesTheEntry()
        {
            metadataStore.Save(new ConnectionMetadata { ConnectionName = "conn1", Database = "Sales", Json = "old" });
            MetadataDialogViewModel viewModel = CreateViewModel();
            viewModel.Initialize("conn1", "Sales");
            viewModel.Json = "   ";

            bool saved = viewModel.TrySave(out string? error);

            Assert.True(saved);
            Assert.Null(error);
            Assert.Null(metadataStore.Get("conn1", "Sales"));
        }

        /// <summary>
        /// Creates the view model over the fakes.
        /// </summary>
        /// <returns>The view model.</returns>
        private MetadataDialogViewModel CreateViewModel()
        {
            return new MetadataDialogViewModel(metadataStore, connectionStore);
        }
    }
}
