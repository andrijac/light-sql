using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// Abstracts user prompts (confirmation, information, error, single-line text
    /// input) and the application's modal dialogs so view models can request them
    /// without referencing WPF directly.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Asks the user a yes/no question.
        /// </summary>
        /// <param name="message">The question to show.</param>
        /// <param name="title">The dialog title.</param>
        /// <returns>True if the user confirmed.</returns>
        bool Confirm(string message, string title);

        /// <summary>
        /// Shows an informational message.
        /// </summary>
        /// <param name="message">The message to show.</param>
        /// <param name="title">The dialog title.</param>
        void ShowInformation(string message, string title);

        /// <summary>
        /// Shows an error message.
        /// </summary>
        /// <param name="message">The message to show.</param>
        /// <param name="title">The dialog title.</param>
        void ShowError(string message, string title);

        /// <summary>
        /// Prompts for a single line of text.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="prompt">The label shown above the input.</param>
        /// <param name="initialValue">The initial text.</param>
        /// <param name="validator">Optional validation returning true for a valid value.</param>
        /// <returns>The entered text, or null if cancelled.</returns>
        string? PromptForText(string title, string prompt, string initialValue = "", Func<string, bool>? validator = null);

        /// <summary>
        /// Shows the connection dialog.
        /// </summary>
        /// <returns>The chosen connection profile, or null if cancelled.</returns>
        ConnectionProfile? ShowConnectionDialog();

        /// <summary>
        /// Shows the options dialog.
        /// </summary>
        /// <returns>True if the user saved changes.</returns>
        bool ShowOptionsDialog();
    }
}
