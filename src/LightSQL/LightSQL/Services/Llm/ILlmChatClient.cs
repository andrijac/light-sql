namespace LightSQL.Services.Llm
{
    /// <summary>
    /// A chat LLM backend (OpenAI-compatible, Anthropic, ...). Takes a request with tools and
    /// returns the model's reply (text and/or tool calls). Providers differ in wire format;
    /// each implementation hides that behind this interface.
    /// </summary>
    public interface ILlmChatClient
    {
        /// <summary>
        /// Sends the conversation and tools to the model and returns its reply.
        /// </summary>
        /// <param name="request">The system prompt, messages and tools.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The model's reply.</returns>
        Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken cancellationToken);
    }
}
