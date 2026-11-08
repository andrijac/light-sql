namespace LightSQL.Services.Tools
{
    /// <summary>
    /// A database tool's name, description and parameters, defined once and used to build the
    /// chat LLM's function-calling schema. The set mirrors the MCP tools in
    /// <c>McpDatabaseTools</c>.
    /// </summary>
    public sealed class ToolDefinition
    {
        /// <summary>
        /// The tool name (e.g. "execute_query").
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// A description shown to the model.
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        /// The tool's parameters.
        /// </summary>
        public IReadOnlyList<ToolParameter> Parameters { get; init; } = Array.Empty<ToolParameter>();
    }
}
