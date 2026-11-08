namespace LightSQL.Models
{
    /// <summary>
    /// A chat provider the in-app chat can use. Every provider speaks the OpenAI-compatible
    /// Chat Completions wire format (with <c>tools</c> / <c>tool_calls</c> function calling),
    /// so one transport serves them all. Values start at 1 so a default (unset) value is never a
    /// valid choice.
    /// </summary>
    public enum LlmProvider
    {
        /// <summary>
        /// Groq Cloud (https://api.groq.com/openai/v1). Hosts open-weight models (GPT-OSS,
        /// Llama) with fast inference and function calling.
        /// </summary>
        Groq = 1,

        /// <summary>
        /// Together AI (https://api.together.xyz/v1). Hosts a large catalogue of open models.
        /// </summary>
        Together = 2,

        /// <summary>
        /// OpenRouter (https://openrouter.ai/api/v1). A gateway that routes to many providers'
        /// models through one key.
        /// </summary>
        OpenRouter = 3,

        /// <summary>
        /// OpenAI (https://api.openai.com/v1). The GPT models directly.
        /// </summary>
        OpenAI = 4,

        /// <summary>
        /// A local OpenAI-compatible server (Ollama, LM Studio, ...) on this machine. Needs no
        /// API key; tool calling depends on the local model.
        /// </summary>
        Local = 5
    }
}
