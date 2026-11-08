using LightSQL.Models;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// One provider's row in the AI Provider dialog: its name, endpoint and where to get a key,
    /// plus whether a key is already saved. The typed key itself is read from the row's PasswordBox
    /// in the dialog code-behind at save time, so it is never held in a bindable property.
    /// </summary>
    public sealed class LlmProviderKeyRow
    {
        /// <summary>
        /// Creates a row from a provider definition.
        /// </summary>
        /// <param name="definition">The provider definition.</param>
        /// <param name="hasExistingKey">Whether a key is already saved for this provider.</param>
        public LlmProviderKeyRow(LlmProviderDefinition definition, bool hasExistingKey)
        {
            Provider = definition.Provider;
            DisplayName = definition.DisplayName;
            BaseUrl = definition.BaseUrl;
            RequiresApiKey = definition.RequiresApiKey;
            ApiKeyUrl = definition.ApiKeyUrl;
            HasExistingKey = hasExistingKey;
        }

        /// <summary>
        /// The provider this row configures.
        /// </summary>
        public LlmProvider Provider { get; }

        /// <summary>
        /// The provider's display name.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// The provider's endpoint root.
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
        /// Whether a key is already saved for this provider.
        /// </summary>
        public bool HasExistingKey { get; }

        /// <summary>
        /// A short description of the key's status for the UI.
        /// </summary>
        public string KeyStatus
        {
            get
            {
                if (RequiresApiKey == false)
                {
                    return "No API key required (local server).";
                }

                return HasExistingKey == true
                    ? "A key is saved. Leave the box blank to keep it."
                    : "No key saved yet.";
            }
        }

        /// <summary>
        /// A "get a key" hint for the UI, or an empty string when the provider needs none.
        /// </summary>
        public string KeyHint
        {
            get
            {
                return string.IsNullOrEmpty(ApiKeyUrl) == true ? string.Empty : $"Get a key: {ApiKeyUrl}";
            }
        }
    }
}
