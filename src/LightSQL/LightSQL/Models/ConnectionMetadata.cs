namespace LightSQL.Models
{
    /// <summary>
    /// Schema and semantic metadata (a JSON document) the user pastes for a database,
    /// so an MCP server locked to that database can pass it to AI clients as extra
    /// instructions. Keyed by connection name and database.
    /// </summary>
    public sealed class ConnectionMetadata
    {
        /// <summary>
        /// The saved connection this metadata belongs to.
        /// </summary>
        public string ConnectionName { get; set; } = string.Empty;

        /// <summary>
        /// The database this metadata describes.
        /// </summary>
        public string Database { get; set; } = string.Empty;

        /// <summary>
        /// The metadata document (JSON) sent to AI clients as extra instructions.
        /// </summary>
        public string Json { get; set; } = string.Empty;

        /// <summary>
        /// When this entry was last saved (UTC).
        /// </summary>
        public DateTime UpdatedUtc { get; set; }
    }
}
