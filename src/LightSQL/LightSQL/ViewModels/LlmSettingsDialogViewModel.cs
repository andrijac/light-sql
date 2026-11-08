using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.Services.Llm;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// Backs the AI Provider dialog: one API key per provider, plus the reply token limit. The
    /// provider and model are chosen in the chat tab, not here. Each key is saved encrypted (DPAPI)
    /// via <see cref="ILlmSettingsStore"/> and is never shown again — leaving a provider's key box
    /// blank keeps its existing key.
    /// </summary>
    public partial class LlmSettingsDialogViewModel : ObservableObject
    {
        private readonly ILlmSettingsStore settingsStore;

        [ObservableProperty]
        private int maxTokens;

        /// <summary>
        /// Creates the view model, seeded from the current settings.
        /// </summary>
        /// <param name="settingsStore">Store the settings are loaded from and saved to.</param>
        public LlmSettingsDialogViewModel(ILlmSettingsStore settingsStore)
        {
            this.settingsStore = settingsStore;

            LlmSettings current = settingsStore.Current;
            maxTokens = current.MaxTokens <= 0 ? 2048 : current.MaxTokens;
            Providers = BuildRows(current);
        }

        /// <summary>
        /// One row per provider (name, endpoint, whether a key is saved).
        /// </summary>
        public ObservableCollection<LlmProviderKeyRow> Providers { get; }

        /// <summary>
        /// Whether the settings were saved when the dialog closed.
        /// </summary>
        public bool Saved { get; private set; }

        /// <summary>
        /// Saves the keys and token limit. For each provider, a non-blank typed key replaces the
        /// stored one; a blank key keeps the existing one.
        /// </summary>
        /// <param name="typedKeys">The key typed per provider (blank/absent to keep the current one).</param>
        /// <param name="error">The reason saving failed, or null on success.</param>
        /// <returns>True when the settings were saved.</returns>
        public bool TrySave(IReadOnlyDictionary<LlmProvider, string?> typedKeys, out string? error)
        {
            LlmSettings updated = settingsStore.Current.Clone();
            updated.MaxTokens = MaxTokens <= 0 ? 2048 : MaxTokens;

            foreach (LlmProviderKeyRow row in Providers)
            {
                if (typedKeys.TryGetValue(row.Provider, out string? typed) == true && string.IsNullOrEmpty(typed) == false)
                {
                    updated.SetApiKey(row.Provider, typed);
                }
            }

            settingsStore.Save(updated);

            Saved = true;
            error = null;

            return true;
        }

        /// <summary>
        /// Builds a row per provider from the catalog, flagging which already have a saved key.
        /// </summary>
        /// <param name="current">The current settings.</param>
        /// <returns>The rows.</returns>
        private static ObservableCollection<LlmProviderKeyRow> BuildRows(LlmSettings current)
        {
            ObservableCollection<LlmProviderKeyRow> rows = new();

            foreach (LlmProviderDefinition definition in LlmProviderCatalog.Providers)
            {
                bool hasKey = string.IsNullOrEmpty(current.GetApiKey(definition.Provider)) == false;
                rows.Add(new LlmProviderKeyRow(definition, hasKey));
            }

            return rows;
        }
    }
}
