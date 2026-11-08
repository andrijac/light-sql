using System.Text.Json.Serialization;

namespace LightSQL.Models
{
    /// <summary>
    /// One provider's stored API key. The plaintext <see cref="ApiKey"/> is held in memory only
    /// (marked <see cref="JsonIgnoreAttribute"/>); only the DPAPI-encrypted
    /// <see cref="ProtectedApiKey"/> is written to disk.
    /// </summary>
    public sealed class LlmProviderCredential
    {
        /// <summary>
        /// The provider this key belongs to.
        /// </summary>
        public LlmProvider Provider { get; set; }

        /// <summary>
        /// The API key, in memory only; never serialized.
        /// </summary>
        [JsonIgnore]
        public string? ApiKey { get; set; }

        /// <summary>
        /// The DPAPI-encrypted API key written to disk.
        /// </summary>
        public string? ProtectedApiKey { get; set; }

        /// <summary>
        /// Returns a copy of the credential.
        /// </summary>
        /// <returns>The copy.</returns>
        public LlmProviderCredential Clone()
        {
            return new LlmProviderCredential
            {
                Provider = Provider,
                ApiKey = ApiKey,
                ProtectedApiKey = ProtectedApiKey
            };
        }
    }
}
