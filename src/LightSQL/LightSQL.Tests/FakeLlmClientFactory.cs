using LightSQL.Models;
using LightSQL.Services.Llm;

namespace LightSQL.Tests
{
    /// <summary>
    /// An <see cref="ILlmClientFactory"/> for view-model tests: hands out one scriptable
    /// <see cref="FakeLlmChatClient"/>, lets a test control whether a provider is "configured",
    /// and records the last remembered selection.
    /// </summary>
    public sealed class FakeLlmClientFactory : ILlmClientFactory
    {
        /// <summary>
        /// The result returned by <see cref="IsConfigured"/> for every provider.
        /// </summary>
        public bool IsConfiguredResult { get; set; } = true;

        /// <summary>
        /// The client returned by <see cref="Create"/>; script its responses in a test.
        /// </summary>
        public FakeLlmChatClient Client { get; } = new();

        /// <summary>
        /// The last provider passed to <see cref="RememberSelection"/>, or null.
        /// </summary>
        public LlmProvider? RememberedProvider { get; private set; }

        /// <summary>
        /// The last model passed to <see cref="RememberSelection"/>, or null.
        /// </summary>
        public string? RememberedModel { get; private set; }

        /// <inheritdoc />
        public LlmProvider? LastProvider { get; set; }

        /// <inheritdoc />
        public string? LastModel { get; set; }

        /// <inheritdoc />
        public bool IsConfigured(LlmProvider provider)
        {
            return IsConfiguredResult;
        }

        /// <inheritdoc />
        public ILlmChatClient Create(LlmProvider provider, string model)
        {
            return Client;
        }

        /// <inheritdoc />
        public void RememberSelection(LlmProvider provider, string model)
        {
            RememberedProvider = provider;
            RememberedModel = model;
        }
    }
}
