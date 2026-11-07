using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.Tests
{
    /// <summary>
    /// In-memory <see cref="IMetadataStore"/> for view-model tests.
    /// </summary>
    public sealed class FakeMetadataStore : IMetadataStore
    {
        /// <summary>
        /// The stored metadata entries.
        /// </summary>
        public List<ConnectionMetadata> Entries { get; } = new();

        /// <inheritdoc />
        public IReadOnlyList<ConnectionMetadata> GetAll()
        {
            return Entries.AsReadOnly();
        }

        /// <inheritdoc />
        public ConnectionMetadata? Get(string connectionName, string? database)
        {
            return Entries.FirstOrDefault(entry =>
                string.Equals(entry.ConnectionName, connectionName, StringComparison.OrdinalIgnoreCase) == true
                && string.Equals(entry.Database, database ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <inheritdoc />
        public void Save(ConnectionMetadata metadata)
        {
            Delete(metadata.ConnectionName, metadata.Database);
            Entries.Add(metadata);
        }

        /// <inheritdoc />
        public void Delete(string connectionName, string? database)
        {
            Entries.RemoveAll(entry =>
                string.Equals(entry.ConnectionName, connectionName, StringComparison.OrdinalIgnoreCase) == true
                && string.Equals(entry.Database, database ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true);
        }
    }
}
