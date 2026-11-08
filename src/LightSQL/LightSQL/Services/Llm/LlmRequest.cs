using LightSQL.Services.Tools;

namespace LightSQL.Services.Llm
{
    /// <summary>
    /// A request to an <see cref="ILlmChatClient"/>: the system prompt, the conversation so
    /// far, and the tools the model may call. The provider, model and credentials belong to
    /// the client, not the request.
    /// </summary>
    public sealed class LlmRequest
    {
        /// <summary>
        /// The system prompt (engine, database, rules and pasted metadata).
        /// </summary>
        public required string SystemPrompt { get; init; }

        /// <summary>
        /// The conversation so far, oldest first.
        /// </summary>
        public required IReadOnlyList<LlmMessage> Messages { get; init; }

        /// <summary>
        /// The tools the model may call.
        /// </summary>
        public IReadOnlyList<ToolDefinition> Tools { get; init; } = Array.Empty<ToolDefinition>();
    }
}
