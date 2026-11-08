using System.Collections.Generic;

namespace LightSQL.Models
{
    /// <summary>
    /// The fixed, preconfigured description of a chat provider: its display name, endpoint root,
    /// whether it needs an API key, where to get one, and a short list of suggested models. This
    /// is reference data (see <c>LlmProviderCatalog</c>), not user settings; the only per-provider
    /// setting the user supplies is the API key (<see cref="LlmProviderCredential"/>).
    /// </summary>
    public sealed class LlmProviderDefinition
    {
        /// <summary>
        /// Creates a provider definition.
        /// </summary>
        /// <param name="provider">The provider identifier.</param>
        /// <param name="displayName">The name shown in the UI.</param>
        /// <param name="baseUrl">The endpoint root, e.g. "https://api.groq.com/openai/v1".</param>
        /// <param name="requiresApiKey">Whether the provider needs an API key (false for local servers).</param>
        /// <param name="apiKeyUrl">A page where the user can create an API key (empty when none).</param>
        /// <param name="models">Suggested model identifiers (the chat's model box is editable).</param>
        /// <param name="usesMaxCompletionTokens">
        /// Whether the provider wants the reply-length cap as <c>max_completion_tokens</c> instead
        /// of the older <c>max_tokens</c>. OpenAI's newer models reject <c>max_tokens</c>; the
        /// other OpenAI-compatible providers still expect it.
        /// </param>
        public LlmProviderDefinition(
            LlmProvider provider,
            string displayName,
            string baseUrl,
            bool requiresApiKey,
            string apiKeyUrl,
            IReadOnlyList<string> models,
            bool usesMaxCompletionTokens = false)
        {
            Provider = provider;
            DisplayName = displayName;
            BaseUrl = baseUrl;
            RequiresApiKey = requiresApiKey;
            ApiKeyUrl = apiKeyUrl;
            Models = models;
            UsesMaxCompletionTokens = usesMaxCompletionTokens;
        }

        /// <summary>
        /// The provider identifier.
        /// </summary>
        public LlmProvider Provider { get; }

        /// <summary>
        /// The name shown in the UI.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// The endpoint root ("/chat/completions" is appended by the client).
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// Whether the provider needs an API key (false for local servers).
        /// </summary>
        public bool RequiresApiKey { get; }

        /// <summary>
        /// A page where the user can create an API key, or an empty string when there is none.
        /// </summary>
        public string ApiKeyUrl { get; }

        /// <summary>
        /// Suggested model identifiers. The chat's model box is editable, so these are starting
        /// points rather than a closed list.
        /// </summary>
        public IReadOnlyList<string> Models { get; }

        /// <summary>
        /// Whether the reply-length cap is sent as <c>max_completion_tokens</c> rather than the
        /// older <c>max_tokens</c>. True for OpenAI (its newer models reject <c>max_tokens</c>);
        /// false for the other OpenAI-compatible providers, which still expect <c>max_tokens</c>.
        /// </summary>
        public bool UsesMaxCompletionTokens { get; }

        /// <summary>
        /// Returns the display name, so the provider can be shown directly in a bound list.
        /// </summary>
        /// <returns>The display name.</returns>
        public override string ToString()
        {
            return DisplayName;
        }
    }
}
