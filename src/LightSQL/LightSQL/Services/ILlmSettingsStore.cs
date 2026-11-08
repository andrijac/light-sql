using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// Persists the in-app chat's LLM provider settings. The API key is stored only in
    /// encrypted form (DPAPI), like connection passwords.
    /// </summary>
    public interface ILlmSettingsStore
    {
        /// <summary>
        /// The current settings (with the API key decrypted into memory for this session).
        /// </summary>
        LlmSettings Current { get; }

        /// <summary>
        /// Saves the settings, encrypting the API key and never writing it in plaintext.
        /// </summary>
        /// <param name="settings">The settings to save.</param>
        void Save(LlmSettings settings);
    }
}
