using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// Persists the database metadata documents the user pastes (keyed by connection
    /// name and database), stored in "%APPDATA%\LightSQL\metadata.json". An MCP server
    /// locked to a database sends the matching document to AI clients as extra
    /// instructions.
    /// </summary>
    public interface IMetadataStore
    {
        /// <summary>
        /// Returns all saved metadata entries.
        /// </summary>
        /// <returns>The saved entries.</returns>
        IReadOnlyList<ConnectionMetadata> GetAll();

        /// <summary>
        /// Returns the metadata for a connection + database, or null when none is saved.
        /// </summary>
        /// <param name="connectionName">The connection name.</param>
        /// <param name="database">The database name (null is treated as empty).</param>
        /// <returns>The matching entry, or null.</returns>
        ConnectionMetadata? Get(string connectionName, string? database);

        /// <summary>
        /// Adds a new entry or replaces the existing one for the same connection + database.
        /// </summary>
        /// <param name="metadata">The entry to save.</param>
        void Save(ConnectionMetadata metadata);

        /// <summary>
        /// Deletes the entry for a connection + database, if present.
        /// </summary>
        /// <param name="connectionName">The connection name.</param>
        /// <param name="database">The database name (null is treated as empty).</param>
        void Delete(string connectionName, string? database);
    }
}
