using LightSQL.Services;
using LightSQL.ViewModels;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the Metadata Extraction Prompt dialog view model: it loads the embedded prompt
    /// and copies it to the clipboard.
    /// </summary>
    public class MetadataPromptDialogViewModelTests
    {
        private readonly FakeClipboardService clipboardService = new();

        [Fact]
        public void Prompt_LoadsTheEmbeddedResource()
        {
            MetadataPromptDialogViewModel viewModel = new(clipboardService);

            Assert.False(string.IsNullOrWhiteSpace(viewModel.Prompt));
            Assert.Contains("DATABASE KNOWLEDGE PACK", viewModel.Prompt);
        }

        [Fact]
        public void Copy_PutsThePromptOnTheClipboard()
        {
            MetadataPromptDialogViewModel viewModel = new(clipboardService);

            viewModel.CopyCommand.Execute(null);

            Assert.Equal(viewModel.Prompt, clipboardService.Text);
            Assert.NotNull(viewModel.CopyMessage);
        }

        [Fact]
        public void EmbeddedPrompt_IsSelfContained()
        {
            string prompt = PromptLibrary.MetadataExtractionPrompt;

            // Generalized: no repo/output placeholders, single database in context.
            Assert.DoesNotContain("{{", prompt);
            Assert.Contains("ONE database in context", prompt);
        }
    }
}
