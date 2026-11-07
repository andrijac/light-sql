namespace LightSQL.Models
{
    /// <summary>
    /// What an MCP server is started with: the connection it exposes, the database
    /// used when a tool call names none, and the callback that records tool calls.
    /// </summary>
    public sealed class McpServerSettings
    {
        /// <summary>
        /// The connection the server opens its own database sessions with.
        /// </summary>
        public required ConnectionProfile Profile { get; init; }

        /// <summary>
        /// The database used when a tool call does not name one; null for the
        /// connection's default database.
        /// </summary>
        public string? DefaultDatabase { get; init; }

        /// <summary>
        /// Extra, user-provided metadata (a JSON document describing the database's
        /// schema and semantics) appended to the server's instructions for AI clients,
        /// or null when none was saved for this connection and database.
        /// </summary>
        public string? MetadataInstructions { get; init; }

        /// <summary>
        /// Returns the current access mode. Called on every query, so the user can
        /// switch between read-only and write-access while the server runs. Defaults
        /// to read-only.
        /// </summary>
        public Func<McpAccessMode> GetAccessMode { get; init; } = () => McpAccessMode.ReadOnly;

        /// <summary>
        /// Called (on a server thread) after every tool call. May be null.
        /// </summary>
        public Action<McpToolCall>? ToolCalled { get; init; }
    }
}
