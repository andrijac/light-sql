using System.IO;
using System.Text.Json;
using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// JSON-backed store of database metadata documents, saved in
    /// "%APPDATA%\LightSQL\metadata.json". Each entry ties a pasted JSON document to a
    /// connection and database; an MCP server locked to that database sends it to AI
    /// clients as extra instructions.
    /// </summary>
    public sealed class MetadataStore : IMetadataStore
    {
        private static readonly JsonSerializerOptions serializerOptions = new()
        {
            WriteIndented = true
        };

        private readonly string filePath;
        private readonly List<ConnectionMetadata> entries;

        /// <summary>
        /// Creates the store using the default metadata file location.
        /// </summary>
        public MetadataStore()
            : this(AppPaths.MetadataFilePath)
        {
        }

        /// <summary>
        /// Creates the store using an explicit file path (used by tests).
        /// </summary>
        /// <param name="filePath">Full path of the metadata JSON file.</param>
        public MetadataStore(string filePath)
        {
            this.filePath = filePath;
            entries = Load();
        }

        /// <inheritdoc />
        public IReadOnlyList<ConnectionMetadata> GetAll()
        {
            return entries.AsReadOnly();
        }

        /// <inheritdoc />
        public ConnectionMetadata? Get(string connectionName, string? database)
        {
            int index = IndexOf(connectionName, database);

            return index >= 0 ? entries[index] : null;
        }

        /// <inheritdoc />
        public void Save(ConnectionMetadata metadata)
        {
            if (metadata is null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            int index = IndexOf(metadata.ConnectionName, metadata.Database);

            if (index >= 0)
            {
                entries[index] = metadata;
            }
            else
            {
                entries.Add(metadata);
            }

            Persist();
        }

        /// <inheritdoc />
        public void Delete(string connectionName, string? database)
        {
            int index = IndexOf(connectionName, database);

            if (index >= 0)
            {
                entries.RemoveAt(index);
                Persist();
            }
        }

        /// <summary>
        /// Finds the entry for a connection + database, or -1 when none matches.
        /// </summary>
        /// <param name="connectionName">The connection name.</param>
        /// <param name="database">The database name (null is treated as empty).</param>
        /// <returns>The index of the entry, or -1.</returns>
        private int IndexOf(string connectionName, string? database)
        {
            return entries.FindIndex(entry =>
                string.Equals(entry.ConnectionName, connectionName, StringComparison.OrdinalIgnoreCase) == true
                && string.Equals(entry.Database, database ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        /// Reads entries from disk, returning an empty list when no file exists or the
        /// file is unreadable.
        /// </summary>
        /// <returns>The loaded entries.</returns>
        private List<ConnectionMetadata> Load()
        {
            if (File.Exists(filePath) == false)
            {
                return new List<ConnectionMetadata>();
            }

            try
            {
                string json = File.ReadAllText(filePath);
                List<ConnectionMetadata>? loaded = JsonSerializer.Deserialize<List<ConnectionMetadata>>(json, serializerOptions);

                return loaded ?? new List<ConnectionMetadata>();
            }
            catch (Exception)
            {
                // A corrupt metadata file should not stop the app from starting.
                return new List<ConnectionMetadata>();
            }
        }

        /// <summary>
        /// Writes all entries to disk.
        /// </summary>
        private void Persist()
        {
            AppPaths.EnsureDirectory(AppPaths.ConfigDirectory);

            string json = JsonSerializer.Serialize(entries, serializerOptions);
            File.WriteAllText(filePath, json);
        }
    }
}
