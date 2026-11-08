using LightSQL.Models;

namespace LightSQL.Services.Llm
{
    /// <summary>
    /// Creates an <see cref="ILlmChatClient"/> for a chosen provider and model, using the saved
    /// per-provider API key. The provider and model are picked in the chat tab, so this factory
    /// builds a client per selection rather than from a single global setting.
    /// </summary>
    public interface ILlmClientFactory
    {
        /// <summary>
        /// The provider used by the most recent chat (for defaulting a new chat tab), or null.
        /// </summary>
        LlmProvider? LastProvider { get; }

        /// <summary>
        /// The model used by the most recent chat (for defaulting a new chat tab), or null.
        /// </summary>
        string? LastModel { get; }

        /// <summary>
        /// Whether the provider is ready to use: it has a saved API key, or it needs none
        /// (a local server).
        /// </summary>
        /// <param name="provider">The provider to check.</param>
        /// <returns>True when a client can be created for it.</returns>
        bool IsConfigured(LlmProvider provider);

        /// <summary>
        /// Creates a chat client for the provider and model.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <param name="model">The model identifier.</param>
        /// <returns>The chat client.</returns>
        ILlmChatClient Create(LlmProvider provider, string model);

        /// <summary>
        /// Remembers the provider and model so a new chat tab defaults to them.
        /// </summary>
        /// <param name="provider">The provider to remember.</param>
        /// <param name="model">The model to remember.</param>
        void RememberSelection(LlmProvider provider, string model);
    }
}
