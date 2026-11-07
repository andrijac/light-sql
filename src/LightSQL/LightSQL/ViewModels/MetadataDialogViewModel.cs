using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// Backs the Database Metadata dialog: the user pastes a JSON metadata document for
    /// a connection + database, and it is saved through <see cref="IMetadataStore"/>.
    /// An MCP server locked to that database sends the document to AI clients as extra
    /// instructions, so this text must not contain secrets.
    /// </summary>
    public partial class MetadataDialogViewModel : ObservableObject
    {
        private readonly IMetadataStore metadataStore;

        [ObservableProperty]
        private string? connectionName;

        [ObservableProperty]
        private string? database;

        [ObservableProperty]
        private string json = string.Empty;

        /// <summary>
        /// Creates the view model.
        /// </summary>
        /// <param name="metadataStore">Store the metadata is loaded from and saved to.</param>
        /// <param name="connectionStore">Supplies the saved connection names offered in the dialog.</param>
        public MetadataDialogViewModel(IMetadataStore metadataStore, IConnectionStore connectionStore)
        {
            this.metadataStore = metadataStore;

            foreach (ConnectionProfile profile in connectionStore.GetAll())
            {
                Connections.Add(profile.Name);
            }
        }

        /// <summary>
        /// The saved connection names offered in the dialog.
        /// </summary>
        public ObservableCollection<string> Connections { get; } = new();

        /// <summary>
        /// Whether the metadata was saved when the dialog closed.
        /// </summary>
        public bool Saved { get; private set; }

        /// <summary>
        /// Prefills the editor with the connection, database and any metadata already
        /// saved for that pair.
        /// </summary>
        /// <param name="connectionName">The connection to prefill.</param>
        /// <param name="database">The database to prefill.</param>
        public void Initialize(string? connectionName, string? database)
        {
            ConnectionName = connectionName;
            Database = database;

            ConnectionMetadata? existing = string.IsNullOrWhiteSpace(connectionName) == true
                ? null
                : metadataStore.Get(connectionName, database);

            Json = existing?.Json ?? string.Empty;
        }

        /// <summary>
        /// Validates and saves the pasted metadata. Empty JSON removes any saved entry.
        /// </summary>
        /// <param name="error">The reason saving failed, or null on success.</param>
        /// <returns>True when the metadata was saved (or cleared); false when invalid.</returns>
        public bool TrySave(out string? error)
        {
            if (string.IsNullOrWhiteSpace(ConnectionName) == true)
            {
                error = "Choose a connection.";

                return false;
            }

            string text = Json ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text) == false && IsValidJson(text) == false)
            {
                error = "The metadata is not valid JSON.";

                return false;
            }

            if (string.IsNullOrWhiteSpace(text) == true)
            {
                metadataStore.Delete(ConnectionName.Trim(), Database?.Trim());
            }
            else
            {
                metadataStore.Save(new ConnectionMetadata
                {
                    ConnectionName = ConnectionName.Trim(),
                    Database = Database?.Trim() ?? string.Empty,
                    Json = text,
                    UpdatedUtc = DateTime.UtcNow
                });
            }

            Saved = true;
            error = null;

            return true;
        }

        /// <summary>
        /// Whether a string parses as a JSON document.
        /// </summary>
        /// <param name="text">The text to check.</param>
        /// <returns>True when the text is well-formed JSON.</returns>
        private static bool IsValidJson(string text)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(text);

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
