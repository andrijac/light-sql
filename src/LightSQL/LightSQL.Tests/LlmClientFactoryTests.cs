using System.IO;
using System.Net.Http;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.Services.Llm;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests <see cref="LlmClientFactory"/>: a key-requiring provider is configured only once a key
    /// is saved, a local provider needs none, it creates the OpenAI-compatible client, and it
    /// remembers the last selection.
    /// </summary>
    public sealed class LlmClientFactoryTests : IDisposable
    {
        private readonly string filePath = Path.Combine(Path.GetTempPath(), $"lightsql-llm-{Guid.NewGuid():N}.json");
        private readonly FakeCredentialProtector protector = new();
        private readonly HttpClient httpClient = new();

        [Fact]
        public void IsConfigured_IsFalse_ForKeyProvider_WithoutKey()
        {
            LlmSettingsStore store = new(protector, filePath);
            LlmClientFactory factory = new(store, httpClient);

            Assert.False(factory.IsConfigured(LlmProvider.Groq));
        }

        [Fact]
        public void IsConfigured_IsTrue_ForLocalProvider_WithoutKey()
        {
            LlmSettingsStore store = new(protector, filePath);
            LlmClientFactory factory = new(store, httpClient);

            Assert.True(factory.IsConfigured(LlmProvider.Local));
        }

        [Fact]
        public void IsConfigured_IsTrue_AndCreatesClient_WhenKeySaved()
        {
            LlmSettings settings = new();
            settings.SetApiKey(LlmProvider.Groq, "k");

            LlmSettingsStore store = new(protector, filePath);
            store.Save(settings);
            LlmClientFactory factory = new(store, httpClient);

            Assert.True(factory.IsConfigured(LlmProvider.Groq));
            Assert.IsType<OpenAiCompatibleChatClient>(factory.Create(LlmProvider.Groq, "openai/gpt-oss-120b"));
        }

        [Fact]
        public void RememberSelection_PersistsLastProviderAndModel()
        {
            LlmSettingsStore store = new(protector, filePath);
            LlmClientFactory factory = new(store, httpClient);

            factory.RememberSelection(LlmProvider.OpenAI, "gpt-5.5");

            LlmClientFactory reloaded = new(new LlmSettingsStore(protector, filePath), httpClient);
            Assert.Equal(LlmProvider.OpenAI, reloaded.LastProvider);
            Assert.Equal("gpt-5.5", reloaded.LastModel);
        }

        /// <summary>
        /// Disposes the HTTP client and deletes the temporary settings file.
        /// </summary>
        public void Dispose()
        {
            httpClient.Dispose();

            if (File.Exists(filePath) == true)
            {
                File.Delete(filePath);
            }
        }
    }
}
