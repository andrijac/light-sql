using LightSQL.Models;
using LightSQL.Services.Llm;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the preconfigured provider catalog: every provider has a usable definition, and only
    /// local providers skip the API key.
    /// </summary>
    public class LlmProviderCatalogTests
    {
        [Fact]
        public void Providers_AreAllWellFormed()
        {
            Assert.NotEmpty(LlmProviderCatalog.Providers);

            foreach (LlmProviderDefinition definition in LlmProviderCatalog.Providers)
            {
                Assert.False(string.IsNullOrWhiteSpace(definition.DisplayName));
                Assert.True(Uri.IsWellFormedUriString(definition.BaseUrl, UriKind.Absolute), $"{definition.Provider} base URL is not absolute.");
                Assert.NotEmpty(definition.Models);
            }
        }

        [Fact]
        public void Get_ReturnsMatchingProvider()
        {
            LlmProviderDefinition? groq = LlmProviderCatalog.Get(LlmProvider.Groq);

            Assert.NotNull(groq);
            Assert.Equal(LlmProvider.Groq, groq!.Provider);
        }

        [Fact]
        public void KeyProviders_RequireAKey_LocalDoesNot()
        {
            Assert.True(LlmProviderCatalog.Get(LlmProvider.Groq)!.RequiresApiKey);
            Assert.True(LlmProviderCatalog.Get(LlmProvider.OpenAI)!.RequiresApiKey);
            Assert.False(LlmProviderCatalog.Get(LlmProvider.Local)!.RequiresApiKey);
        }

        [Fact]
        public void OnlyOpenAi_UsesMaxCompletionTokens()
        {
            Assert.True(LlmProviderCatalog.Get(LlmProvider.OpenAI)!.UsesMaxCompletionTokens);

            foreach (LlmProviderDefinition definition in LlmProviderCatalog.Providers)
            {
                if (definition.Provider != LlmProvider.OpenAI)
                {
                    Assert.False(definition.UsesMaxCompletionTokens, $"{definition.Provider} should send max_tokens.");
                }
            }
        }
    }
}
