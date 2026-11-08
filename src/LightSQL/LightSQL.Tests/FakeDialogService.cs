using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.Tests
{
    /// <summary>
    /// Scriptable <see cref="IDialogService"/> for view-model tests: returns preset
    /// answers and counts how often each prompt was shown.
    /// </summary>
    public sealed class FakeDialogService : IDialogService
    {
        /// <summary>
        /// The answer returned by <see cref="Confirm"/>.
        /// </summary>
        public bool ConfirmResult { get; set; }

        /// <summary>
        /// How many times <see cref="Confirm"/> was called.
        /// </summary>
        public int ConfirmCount { get; private set; }

        /// <summary>
        /// The answer returned by <see cref="ConfirmSaveChanges"/>.
        /// </summary>
        public SaveChangesChoice SaveChangesResult { get; set; } = SaveChangesChoice.Cancel;

        /// <summary>
        /// The query names passed to <see cref="ConfirmSaveChanges"/>, in order.
        /// </summary>
        public List<string> SaveChangesPrompts { get; } = new();

        /// <summary>
        /// The text returned by <see cref="PromptForText"/> (null simulates Cancel).
        /// </summary>
        public string? PromptResult { get; set; }

        /// <summary>
        /// The paths returned by <see cref="ShowOpenFileDialog"/> (empty simulates Cancel).
        /// </summary>
        public List<string> OpenFileResult { get; } = new();

        /// <summary>
        /// The initial folders passed to <see cref="ShowOpenFileDialog"/>, in order.
        /// </summary>
        public List<string?> OpenFileDirectories { get; } = new();

        /// <summary>
        /// The path returned by <see cref="ShowSaveFileDialog"/> (null simulates Cancel).
        /// </summary>
        public string? SaveFileResult { get; set; }

        /// <summary>
        /// The suggested names and initial folders passed to <see cref="ShowSaveFileDialog"/>, in order.
        /// </summary>
        public List<(string SuggestedFileName, string? InitialDirectory)> SaveFilePrompts { get; } = new();

        /// <summary>
        /// How many times <see cref="ShowInformation"/> was called.
        /// </summary>
        public int InformationCount { get; private set; }

        /// <summary>
        /// How many times <see cref="ShowError"/> was called.
        /// </summary>
        public int ErrorCount { get; private set; }

        /// <summary>
        /// The profile returned by <see cref="ShowConnectionDialog"/>.
        /// </summary>
        public ConnectionProfile? ConnectionProfileResult { get; set; }

        /// <inheritdoc />
        public bool Confirm(string message, string title)
        {
            ConfirmCount++;

            return ConfirmResult;
        }

        /// <inheritdoc />
        public SaveChangesChoice ConfirmSaveChanges(string queryName)
        {
            SaveChangesPrompts.Add(queryName);

            return SaveChangesResult;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> ShowOpenFileDialog(string? initialDirectory)
        {
            OpenFileDirectories.Add(initialDirectory);

            return OpenFileResult.ToList();
        }

        /// <inheritdoc />
        public string? ShowSaveFileDialog(string suggestedFileName, string? initialDirectory)
        {
            SaveFilePrompts.Add((suggestedFileName, initialDirectory));

            return SaveFileResult;
        }

        /// <inheritdoc />
        public void ShowInformation(string message, string title)
        {
            InformationCount++;
        }

        /// <inheritdoc />
        public void ShowError(string message, string title)
        {
            ErrorCount++;
        }

        /// <inheritdoc />
        public string? PromptForText(string title, string prompt, string initialValue = "", Func<string, bool>? validator = null)
        {
            return PromptResult;
        }

        /// <inheritdoc />
        public ConnectionProfile? ShowConnectionDialog()
        {
            return ConnectionProfileResult;
        }

        /// <inheritdoc />
        public bool ShowOptionsDialog()
        {
            return false;
        }

        /// <summary>
        /// The result returned by <see cref="ShowMetadataDialog"/>.
        /// </summary>
        public bool MetadataDialogResult { get; set; }

        /// <summary>
        /// How many times <see cref="ShowMetadataDialog"/> was called.
        /// </summary>
        public int MetadataDialogCount { get; private set; }

        /// <summary>
        /// The connection name passed to the last <see cref="ShowMetadataDialog"/> call.
        /// </summary>
        public string? MetadataDialogConnectionName { get; private set; }

        /// <summary>
        /// The database passed to the last <see cref="ShowMetadataDialog"/> call.
        /// </summary>
        public string? MetadataDialogDatabase { get; private set; }

        /// <inheritdoc />
        public bool ShowMetadataDialog(string? connectionName, string? database)
        {
            MetadataDialogCount++;
            MetadataDialogConnectionName = connectionName;
            MetadataDialogDatabase = database;

            return MetadataDialogResult;
        }

        /// <summary>
        /// How many times <see cref="ShowMetadataPromptDialog"/> was called.
        /// </summary>
        public int MetadataPromptDialogCount { get; private set; }

        /// <inheritdoc />
        public void ShowMetadataPromptDialog()
        {
            MetadataPromptDialogCount++;
        }

        /// <summary>
        /// The result returned by <see cref="ShowLlmSettingsDialog"/>.
        /// </summary>
        public bool LlmSettingsDialogResult { get; set; }

        /// <summary>
        /// How many times <see cref="ShowLlmSettingsDialog"/> was called.
        /// </summary>
        public int LlmSettingsDialogCount { get; private set; }

        /// <inheritdoc />
        public bool ShowLlmSettingsDialog()
        {
            LlmSettingsDialogCount++;

            return LlmSettingsDialogResult;
        }
    }
}
