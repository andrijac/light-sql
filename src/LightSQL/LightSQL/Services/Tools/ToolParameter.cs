namespace LightSQL.Services.Tools
{
    /// <summary>
    /// One parameter of a database tool, used to build an LLM function-calling schema.
    /// </summary>
    public sealed class ToolParameter
    {
        /// <summary>
        /// The parameter name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// The JSON-schema type ("string" or "integer").
        /// </summary>
        public required string Type { get; init; }

        /// <summary>
        /// A description shown to the model.
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        /// Whether the model must supply this parameter.
        /// </summary>
        public bool Required { get; init; }
    }
}
