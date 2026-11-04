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
    }
}
