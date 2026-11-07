using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightSQL.Services;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// Backs the Metadata Extraction Prompt dialog: shows the embedded prompt and copies it
    /// to the clipboard. An AI agent runs the prompt against a code repository to generate the
    /// JSON the user pastes into the Database Metadata dialog.
    /// </summary>
    public partial class MetadataPromptDialogViewModel : ObservableObject
    {
        private readonly IClipboardService clipboardService;

        [ObservableProperty]
        private string? copyMessage;

        /// <summary>
        /// Creates the view model.
        /// </summary>
        /// <param name="clipboardService">Copies the prompt to the clipboard.</param>
        public MetadataPromptDialogViewModel(IClipboardService clipboardService)
        {
            this.clipboardService = clipboardService;
            Prompt = PromptLibrary.MetadataExtractionPrompt;
        }

        /// <summary>
        /// The extraction prompt text shown in the dialog.
        /// </summary>
        public string Prompt { get; }

        /// <summary>
        /// Copies the prompt to the clipboard.
        /// </summary>
        [RelayCommand]
        private void Copy()
        {
            clipboardService.SetText(Prompt);
            CopyMessage = "Prompt copied to the clipboard.";
        }
    }
}
