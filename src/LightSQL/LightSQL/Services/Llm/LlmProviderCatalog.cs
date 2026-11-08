using System.Collections.Generic;
using System.Linq;
using LightSQL.Models;

namespace LightSQL.Services.Llm
{
    /// <summary>
    /// The fixed set of chat providers the in-app chat supports, with their endpoints and a few
    /// suggested models. All of them speak the OpenAI-compatible Chat Completions format with
    /// function calling, so one transport (<see cref="OpenAiCompatibleChatClient"/>) serves them
    /// all. The suggested model lists are starting points: the chat's model box is editable, and
    /// providers add and retire models over time.
    /// <para>
    /// The suggested models were checked against each provider's live catalogue on 2026-11-08
    /// and limited to models that are currently available and support tool/function calling
    /// through plain Chat Completions (the chat is an agentic tool-caller). Re-check them when
    /// they start to drift; the provider's own model list is the final authority.
    /// </para>
    /// </summary>
    public static class LlmProviderCatalog
    {
        private static readonly IReadOnlyList<LlmProviderDefinition> providers = BuildProviders();

        /// <summary>
        /// Every supported provider, in display order.
        /// </summary>
        public static IReadOnlyList<LlmProviderDefinition> Providers
        {
            get
            {
                return providers;
            }
        }

        /// <summary>
        /// Finds a provider definition by its identifier.
        /// </summary>
        /// <param name="provider">The provider to look up.</param>
        /// <returns>The definition, or null when it is not in the catalog.</returns>
        public static LlmProviderDefinition? Get(LlmProvider provider)
        {
            return providers.FirstOrDefault(definition => definition.Provider == provider);
        }

        /// <summary>
        /// Builds the provider catalogue.
        /// </summary>
        /// <returns>The provider definitions.</returns>
        private static IReadOnlyList<LlmProviderDefinition> BuildProviders()
        {
            return new List<LlmProviderDefinition>
            {
                new LlmProviderDefinition(
                    LlmProvider.Groq,
                    "Groq",
                    "https://api.groq.com/openai/v1",
                    true,
                    "https://console.groq.com/keys",
                    new[]
                    {
                        // Developer-tier production models with tool use. The Llama 3/4 models
                        // the list used to carry are retired or enterprise-only now; gpt-oss is
                        // what a standard key can reach. qwen3.8-27b is preview but callable.
                        "openai/gpt-oss-120b",
                        "openai/gpt-oss-20b",
                        "qwen/qwen3.8-27b"
                    }),
                new LlmProviderDefinition(
                    LlmProvider.Together,
                    "Together AI",
                    "https://api.together.xyz/v1",
                    true,
                    "https://api.together.ai/settings/api-keys",
                    new[]
                    {
                        // gpt-oss-120b is confirmed in the serverless catalogue; the rest are
                        // Together's current recommended tool-calling models (the Llama 4,
                        // Qwen2.5 and DeepSeek-V3 entries that used to be here are retired).
                        "openai/gpt-oss-120b",
                        "zai-org/GLM-5.3",
                        "zai-org/GLM-5.3-Flash",
                        "deepseek-ai/DeepSeek-V4.1-Flash-1017",
                        "Qwen/Qwen3.8-Flash"
                    }),
                new LlmProviderDefinition(
                    LlmProvider.OpenRouter,
                    "OpenRouter",
                    "https://openrouter.ai/api/v1",
                    true,
                    "https://openrouter.ai/keys",
                    new[]
                    {
                        // Verified against OpenRouter's live /api/v1/models (all have "tools" in
                        // supported_parameters). A varied mix across providers.
                        "anthropic/claude-sonnet-5.5",
                        "openai/gpt-5.1",
                        "openai/gpt-oss-120b",
                        "google/gemini-3.5-flash-lite",
                        "deepseek/deepseek-v3.2",
                        "qwen/qwen3-235b-a22b-2507"
                    }),
                new LlmProviderDefinition(
                    LlmProvider.OpenAI,
                    "OpenAI",
                    "https://api.openai.com/v1",
                    true,
                    "https://platform.openai.com/api-keys",
                    new[]
                    {
                        // GPT-5.x models that do native tool calling on Chat Completions with
                        // reasoning on. The GPT-6 family is deliberately left out: it needs
                        // reasoning_effort="none" for tools (or the Responses API), which this
                        // plain Chat Completions transport does not send. The old GPT-4.x/4o and
                        // o4-mini IDs are deprecated or shut down.
                        "gpt-5.5",
                        "gpt-5.4",
                        "gpt-5.4-mini",
                        "gpt-5.2"
                    },
                    // OpenAI's newer models reject "max_tokens" and want "max_completion_tokens".
                    usesMaxCompletionTokens: true),
                new LlmProviderDefinition(
                    LlmProvider.Local,
                    "Local (Ollama / LM Studio)",
                    "http://localhost:11434/v1",
                    false,
                    string.Empty,
                    new[]
                    {
                        "llama3.1",
                        "llama3.3",
                        "qwen2.5",
                        "qwen2.5-coder"
                    })
            };
        }
    }
}
