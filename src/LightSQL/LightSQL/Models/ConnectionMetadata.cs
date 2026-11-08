namespace LightSQL.Models
{
    /// <summary>
    /// Schema and semantic metadata (a JSON, Markdown or free-text document) the user
    /// pastes for a database, so an MCP server locked to that database — and the in-app
    /// chat — can pass it to the AI as extra instructions. Keyed by connection name and
    /// database.
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
        /// The metadata document sent to the AI as extra instructions. It can be JSON,
        /// Markdown or any text; it is appended verbatim and never parsed. The property is
        /// named "Json" for historical reasons and to keep existing "metadata.json" files
        /// readable.
        /// </summary>
        public string Json { get; set; } = string.Empty;

        /// <summary>
        /// When this entry was last saved (UTC).
        /// </summary>
        public DateTime UpdatedUtc { get; set; }
    }
}
