namespace LightSQL.Services.Llm
{
    /// <summary>
    /// A reply from an <see cref="ILlmChatClient"/>: assistant text and/or tool calls. When
    /// there are tool calls, the caller runs them and sends the results back for another turn.
    /// </summary>
    public sealed class LlmResponse
    {
        /// <summary>
        /// The assistant's text, or null when the turn is only tool calls.
        /// </summary>
        public string? Text { get; init; }

        /// <summary>
        /// The tool calls the model asked for (empty when it answered directly).
        /// </summary>
        public IReadOnlyList<LlmToolCall> ToolCalls { get; init; } = Array.Empty<LlmToolCall>();
    }
}
