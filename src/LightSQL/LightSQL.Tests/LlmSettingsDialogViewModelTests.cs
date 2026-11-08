using System.Collections.Generic;
using System.IO;
using System.Linq;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.ViewModels;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the AI Provider dialog view model: it offers a row per provider, saves a typed key
    /// per provider (encrypted), and keeps an existing key when its box is left blank.
    /// </summary>
    public sealed class LlmSettingsDialogViewModelTests : IDisposable
    {
        private readonly string filePath = Path.Combine(Path.GetTempPath(), $"lightsql-llm-{Guid.NewGuid():N}.json");
        private readonly FakeCredentialProtector protector = new();

        [Fact]
        public void Providers_HasOneRowPerCatalogProvider()
        {
            LlmSettingsStore store = new(protector, filePath);
            LlmSettingsDialogViewModel viewModel = new(store);

            Assert.Equal(LightSQL.Services.Llm.LlmProviderCatalog.Providers.Count, viewModel.Providers.Count);
        }

        [Fact]
        public void TrySave_StoresTypedKeyPerProvider()
        {
            LlmSettingsStore store = new(protector, filePath);
            LlmSettingsDialogViewModel viewModel = new(store) { MaxTokens = 1234 };

            Dictionary<LlmProvider, string?> typed = new() { [LlmProvider.Groq] = "groq-key" };
            bool saved = viewModel.TrySave(typed, out string? error);

            Assert.True(saved);
            Assert.Null(error);
            Assert.Equal("groq-key", store.Current.GetApiKey(LlmProvider.Groq));
            Assert.Equal(1234, store.Current.MaxTokens);
        }

        [Fact]
        public void TrySave_BlankKey_KeepsExistingKey()
        {
            LlmSettings initial = new();
            initial.SetApiKey(LlmProvider.Groq, "existing");
            LlmSettingsStore store = new(protector, filePath);
            store.Save(initial);

            LlmSettingsDialogViewModel viewModel = new(store);
            Dictionary<LlmProvider, string?> typed = new() { [LlmProvider.Groq] = string.Empty };
            viewModel.TrySave(typed, out _);

            Assert.Equal("existing", store.Current.GetApiKey(LlmProvider.Groq));
        }

        [Fact]
        public void Providers_FlagsExistingKey()
        {
            LlmSettings initial = new();
            initial.SetApiKey(LlmProvider.OpenAI, "k");
            LlmSettingsStore store = new(protector, filePath);
            store.Save(initial);

            LlmSettingsDialogViewModel viewModel = new(store);
            LlmProviderKeyRow openAi = viewModel.Providers.Single(row => row.Provider == LlmProvider.OpenAI);

            Assert.True(openAi.HasExistingKey);
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
