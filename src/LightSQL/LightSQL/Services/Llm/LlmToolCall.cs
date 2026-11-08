namespace LightSQL.Services.Llm
{
    /// <summary>
    /// A tool call the model asked for: its id (echoed back with the result), the tool name
    /// and the raw JSON arguments.
    /// </summary>
    public sealed class LlmToolCall
    {
        /// <summary>
        /// The provider-assigned id, echoed back when the result is returned.
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// The tool name (see <c>DatabaseToolCatalog</c>).
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// The arguments as a raw JSON object string.
        /// </summary>
        public required string ArgumentsJson { get; init; }
    }
}
