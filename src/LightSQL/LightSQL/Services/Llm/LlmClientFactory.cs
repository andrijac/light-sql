using System.Net.Http;
using LightSQL.Models;

namespace LightSQL.Services.Llm
{
    /// <summary>
    /// Builds an <see cref="ILlmChatClient"/> for a chosen provider and model, reading that
    /// provider's endpoint from <see cref="LlmProviderCatalog"/> and its API key from the saved
    /// settings. Every provider uses the OpenAI-compatible transport, so all clients share one
    /// <see cref="HttpClient"/>.
    /// </summary>
    public sealed class LlmClientFactory : ILlmClientFactory
    {
        private readonly ILlmSettingsStore settingsStore;
        private readonly HttpClient httpClient;

        /// <summary>
        /// Creates the factory.
        /// </summary>
        /// <param name="settingsStore">Supplies the current LLM settings (keys, last selection).</param>
        /// <param name="httpClient">The shared HTTP client.</param>
        public LlmClientFactory(ILlmSettingsStore settingsStore, HttpClient httpClient)
        {
            this.settingsStore = settingsStore;
            this.httpClient = httpClient;
        }

        /// <inheritdoc />
        public LlmProvider? LastProvider
        {
            get
            {
                return settingsStore.Current.LastProvider;
            }
        }

        /// <inheritdoc />
        public string? LastModel
        {
            get
            {
                return settingsStore.Current.LastModel;
            }
        }

        /// <inheritdoc />
        public bool IsConfigured(LlmProvider provider)
        {
            LlmProviderDefinition? definition = LlmProviderCatalog.Get(provider);

            if (definition is null)
            {
                return false;
            }

            if (definition.RequiresApiKey == false)
            {
                return true;
            }

            return string.IsNullOrWhiteSpace(settingsStore.Current.GetApiKey(provider)) == false;
        }

        /// <inheritdoc />
        public ILlmChatClient Create(LlmProvider provider, string model)
        {
            LlmProviderDefinition definition = LlmProviderCatalog.Get(provider)
                ?? throw new NotSupportedException($"The LLM provider '{provider}' is not in the catalog.");

            string apiKey = settingsStore.Current.GetApiKey(provider) ?? string.Empty;

            return new OpenAiCompatibleChatClient(
                httpClient,
                definition.BaseUrl,
                apiKey,
                model,
                settingsStore.Current.MaxTokens,
                definition.UsesMaxCompletionTokens);
        }

        /// <inheritdoc />
        public void RememberSelection(LlmProvider provider, string model)
        {
            LlmSettings current = settingsStore.Current;

            if (current.LastProvider == provider && current.LastModel == model)
            {
                return;
            }

            LlmSettings updated = current.Clone();
            updated.LastProvider = provider;
            updated.LastModel = model;

            settingsStore.Save(updated);
        }
    }
}
