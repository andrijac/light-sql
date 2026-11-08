using System.Collections.Generic;
using System.Linq;

namespace LightSQL.Models
{
    /// <summary>
    /// The in-app chat's saved settings: one API key per provider (each encrypted with DPAPI,
    /// see <see cref="LlmProviderCredential"/>), the last provider and model the user picked (so a
    /// new chat tab defaults to them), and the reply token limit. The provider and model are chosen
    /// in the chat tab, not here; this type only stores the keys and the remembered selection.
    /// </summary>
    public sealed class LlmSettings
    {
        /// <summary>
        /// The stored API keys, one per provider that the user has configured.
        /// </summary>
        public List<LlmProviderCredential> Credentials { get; set; } = new();

        /// <summary>
        /// The provider used by the most recent chat, so new chat tabs default to it; null when
        /// the user has not chatted yet.
        /// </summary>
        public LlmProvider? LastProvider { get; set; }

        /// <summary>
        /// The model used by the most recent chat, so new chat tabs default to it.
        /// </summary>
        public string? LastModel { get; set; }

        /// <summary>
        /// The maximum tokens generated per reply.
        /// </summary>
        public int MaxTokens { get; set; } = 2048;

        /// <summary>
        /// Finds the stored credential for a provider.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <returns>The credential, or null when none is stored.</returns>
        public LlmProviderCredential? FindCredential(LlmProvider provider)
        {
            return Credentials.FirstOrDefault(credential => credential.Provider == provider);
        }

        /// <summary>
        /// Returns a provider's decrypted API key, or null when none is stored.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <returns>The key, or null.</returns>
        public string? GetApiKey(LlmProvider provider)
        {
            return FindCredential(provider)?.ApiKey;
        }

        /// <summary>
        /// Sets (or adds) a provider's API key in memory.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <param name="apiKey">The plaintext key.</param>
        public void SetApiKey(LlmProvider provider, string? apiKey)
        {
            LlmProviderCredential? credential = FindCredential(provider);

            if (credential is null)
            {
                credential = new LlmProviderCredential { Provider = provider };
                Credentials.Add(credential);
            }

            credential.ApiKey = apiKey;
        }

        /// <summary>
        /// Returns a deep copy of the settings.
        /// </summary>
        /// <returns>The copy.</returns>
        public LlmSettings Clone()
        {
            return new LlmSettings
            {
                Credentials = Credentials.Select(credential => credential.Clone()).ToList(),
                LastProvider = LastProvider,
                LastModel = LastModel,
                MaxTokens = MaxTokens
            };
        }
    }
}
