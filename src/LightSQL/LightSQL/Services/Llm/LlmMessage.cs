namespace LightSQL.Services.Llm
{
    /// <summary>
    /// One message in a chat conversation: a user message, an assistant reply (text and/or
    /// tool calls), or a tool result.
    /// </summary>
    public sealed class LlmMessage
    {
        /// <summary>
        /// Who authored the message.
        /// </summary>
        public required LlmRole Role { get; init; }

        /// <summary>
        /// The text content, or null (an assistant turn may have only tool calls).
        /// </summary>
        public string? Text { get; init; }

        /// <summary>
        /// The tool calls the assistant asked for (empty for user and tool messages).
        /// </summary>
        public IReadOnlyList<LlmToolCall> ToolCalls { get; init; } = Array.Empty<LlmToolCall>();

        /// <summary>
        /// For a tool result (<see cref="LlmRole.Tool"/>), the id of the tool call it answers.
        /// </summary>
        public string? ToolCallId { get; init; }
    }
}
