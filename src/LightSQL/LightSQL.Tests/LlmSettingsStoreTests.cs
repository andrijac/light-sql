using System.IO;
using LightSQL.Models;
using LightSQL.Services;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the DPAPI-backed <see cref="LlmSettingsStore"/>: defaults, round-tripping the
    /// per-provider keys and the remembered selection, and that keys are encrypted on disk and
    /// decrypted back into memory.
    /// </summary>
    public sealed class LlmSettingsStoreTests : IDisposable
    {
        private readonly string filePath = Path.Combine(Path.GetTempPath(), $"lightsql-llm-{Guid.NewGuid():N}.json");
        private readonly FakeCredentialProtector protector = new();

        [Fact]
        public void Current_ReturnsDefaults_WhenNoFile()
        {
            LlmSettingsStore store = new(protector, filePath);

            Assert.Empty(store.Current.Credentials);
            Assert.Null(store.Current.LastProvider);
            Assert.Equal(2048, store.Current.MaxTokens);
        }

        [Fact]
        public void Save_ThenReload_RoundTrips_AndEncryptsEachKey()
        {
            LlmSettings settings = new() { MaxTokens = 1000, LastProvider = LlmProvider.Groq, LastModel = "openai/gpt-oss-120b" };
            settings.SetApiKey(LlmProvider.Groq, "groq-secret");
            settings.SetApiKey(LlmProvider.OpenAI, "openai-secret");

            LlmSettingsStore store = new(protector, filePath);
            store.Save(settings);

            string fileText = File.ReadAllText(filePath);
            Assert.DoesNotContain("groq-secret", fileText);
            Assert.DoesNotContain("openai-secret", fileText);

            LlmSettingsStore reloaded = new(protector, filePath);
            Assert.Equal(1000, reloaded.Current.MaxTokens);
            Assert.Equal(LlmProvider.Groq, reloaded.Current.LastProvider);
            Assert.Equal("openai/gpt-oss-120b", reloaded.Current.LastModel);
            Assert.Equal("groq-secret", reloaded.Current.GetApiKey(LlmProvider.Groq));
            Assert.Equal("openai-secret", reloaded.Current.GetApiKey(LlmProvider.OpenAI));
        }

        [Fact]
        public void Save_WithoutKey_WritesNoProtectedKey()
        {
            LlmSettings settings = new();
            settings.SetApiKey(LlmProvider.Groq, null);

            LlmSettingsStore store = new(protector, filePath);
            store.Save(settings);

            LlmSettingsStore reloaded = new(protector, filePath);
            Assert.True(string.IsNullOrEmpty(reloaded.Current.GetApiKey(LlmProvider.Groq)));
            Assert.Null(reloaded.Current.FindCredential(LlmProvider.Groq)?.ProtectedApiKey);
        }

        /// <summary>
        /// Deletes the temporary settings file.
        /// </summary>
        public void Dispose()
        {
            if (File.Exists(filePath) == true)
            {
                File.Delete(filePath);
            }
        }
    }
}
