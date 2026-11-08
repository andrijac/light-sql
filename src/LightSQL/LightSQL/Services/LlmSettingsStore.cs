using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// JSON-backed store of <see cref="LlmSettings"/>, saved in "%APPDATA%\LightSQL\llm.json".
    /// Each provider's API key is encrypted with <see cref="ICredentialProtector"/> (DPAPI) and
    /// written only as <see cref="LlmProviderCredential.ProtectedApiKey"/>; plaintext keys are
    /// never persisted.
    /// </summary>
    public sealed class LlmSettingsStore : ILlmSettingsStore
    {
        private static readonly JsonSerializerOptions serializerOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly ICredentialProtector credentialProtector;
        private readonly string filePath;
        private LlmSettings current;

        /// <summary>
        /// Creates the store using the default settings file location.
        /// </summary>
        /// <param name="credentialProtector">Protector used to encrypt/decrypt the API key.</param>
        public LlmSettingsStore(ICredentialProtector credentialProtector)
            : this(credentialProtector, AppPaths.LlmSettingsFilePath)
        {
        }

        /// <summary>
        /// Creates the store using an explicit file path (used by tests).
        /// </summary>
        /// <param name="credentialProtector">Protector used to encrypt/decrypt the API key.</param>
        /// <param name="filePath">Full path of the settings JSON file.</param>
        public LlmSettingsStore(ICredentialProtector credentialProtector, string filePath)
        {
            this.credentialProtector = credentialProtector;
            this.filePath = filePath;
            current = Load();
        }

        /// <inheritdoc />
        public LlmSettings Current
        {
            get
            {
                return current;
            }
        }

        /// <inheritdoc />
        public void Save(LlmSettings settings)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            LlmSettings toPersist = settings.Clone();

            foreach (LlmProviderCredential credential in toPersist.Credentials)
            {
                credential.ProtectedApiKey = string.IsNullOrEmpty(credential.ApiKey) == false
                    ? credentialProtector.Protect(credential.ApiKey)
                    : null;

                // The plaintext key is [JsonIgnore], but clear it defensively before writing.
                credential.ApiKey = null;
            }

            AppPaths.EnsureDirectory(AppPaths.ConfigDirectory);
            File.WriteAllText(filePath, JsonSerializer.Serialize(toPersist, serializerOptions));

            // Keep the plaintext key in the in-memory copy for this session.
            current = settings.Clone();
        }

        /// <summary>
        /// Reads the settings from disk, decrypting the API key, returning defaults when no
        /// file exists or it is unreadable.
        /// </summary>
        /// <returns>The loaded settings.</returns>
        private LlmSettings Load()
        {
            if (File.Exists(filePath) == false)
            {
                return new LlmSettings();
            }

            try
            {
                string json = File.ReadAllText(filePath);
                LlmSettings? loaded = JsonSerializer.Deserialize<LlmSettings>(json, serializerOptions);

                if (loaded is null)
                {
                    return new LlmSettings();
                }

                // An older or corrupt file may omit the list; never hand back a null collection.
                loaded.Credentials ??= new List<LlmProviderCredential>();

                foreach (LlmProviderCredential credential in loaded.Credentials)
                {
                    if (string.IsNullOrEmpty(credential.ProtectedApiKey) == true)
                    {
                        continue;
                    }

                    try
                    {
                        credential.ApiKey = credentialProtector.Unprotect(credential.ProtectedApiKey);
                    }
                    catch (Exception)
                    {
                        // A key encrypted by another user/machine cannot be decrypted; treat
                        // it as absent but keep the rest of the settings.
                        credential.ApiKey = null;
                    }
                }

                return loaded;
            }
            catch (Exception)
            {
                // A corrupt settings file should not stop the app from starting.
                return new LlmSettings();
            }
        }
    }
}
