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
        /// Asks whether to save a query that has unsaved changes before closing it.
        /// </summary>
        /// <param name="queryName">The display name of the query (its file name).</param>
        /// <returns>Save, Discard (don't save) or Cancel (keep it open).</returns>
        SaveChangesChoice ConfirmSaveChanges(string queryName);

        /// <summary>
        /// Shows the standard "Open" file dialog for SQL files. Several files can be
        /// selected at once.
        /// </summary>
        /// <param name="initialDirectory">The folder to start in, or null to let Windows decide.</param>
        /// <returns>The selected file paths; empty when cancelled.</returns>
        IReadOnlyList<string> ShowOpenFileDialog(string? initialDirectory);

        /// <summary>
        /// Shows the standard "Save As" file dialog for a SQL file. Windows asks
        /// before overwriting an existing file.
        /// </summary>
        /// <param name="suggestedFileName">The file name proposed in the dialog.</param>
        /// <param name="initialDirectory">The folder to start in, or null to let Windows decide.</param>
        /// <returns>The chosen file path, or null when cancelled.</returns>
        string? ShowSaveFileDialog(string suggestedFileName, string? initialDirectory);

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

        /// <summary>
        /// Shows the Database Metadata dialog, prefilled for a connection + database, so
        /// the user can paste the JSON metadata sent to AI clients by an MCP server.
        /// </summary>
        /// <param name="connectionName">The connection to prefill, or null.</param>
        /// <param name="database">The database to prefill, or null.</param>
        /// <returns>True if the user saved the metadata.</returns>
        bool ShowMetadataDialog(string? connectionName, string? database);

        /// <summary>
        /// Shows the Metadata Extraction Prompt dialog (the embedded prompt plus a Copy button).
        /// </summary>
        void ShowMetadataPromptDialog();
    }
}
