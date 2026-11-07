using System.IO;
using System.Windows;
using LightSQL.Models;
using LightSQL.ViewModels;
using LightSQL.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace LightSQL.Services
{
    /// <summary>
    /// WPF implementation of <see cref="IDialogService"/> using standard message
    /// boxes, the standard Windows Open / Save As file dialogs, the reusable
    /// <see cref="InputDialog"/>, and the application's modal dialog windows resolved
    /// from the service provider.
    /// </summary>
    public sealed class DialogService : IDialogService
    {
        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// File-type filter of the Open / Save As dialogs.
        /// </summary>
        private const string SqlFileFilter = "SQL files (*.sql)|*.sql|Text files (*.txt)|*.txt|All files (*.*)|*.*";

        /// <summary>
        /// Extension added to a file name typed without one in Save As.
        /// </summary>
        private const string SqlFileExtension = ".sql";

        /// <summary>
        /// Creates the dialog service.
        /// </summary>
        /// <param name="serviceProvider">Provider used to resolve dialog view models.</param>
        public DialogService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public bool Confirm(string message, string title)
        {
            Window? owner = GetOwner();

            MessageBoxResult result = owner is null
                ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                : MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            return result == MessageBoxResult.Yes;
        }

        /// <inheritdoc />
        public SaveChangesChoice ConfirmSaveChanges(string queryName)
        {
            Window? owner = GetOwner();
            string message = $"Save changes to '{queryName}'?";
            const string title = "LightSQL";

            MessageBoxResult result = owner is null
                ? MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes)
                : MessageBox.Show(owner, message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);

            switch (result)
            {
                case MessageBoxResult.Yes:
                {
                    return SaveChangesChoice.Save;
                }

                case MessageBoxResult.No:
                {
                    return SaveChangesChoice.Discard;
                }

                default:
                {
                    return SaveChangesChoice.Cancel;
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<string> ShowOpenFileDialog(string? initialDirectory)
        {
            OpenFileDialog dialog = new()
            {
                Title = "Open File",
                Filter = SqlFileFilter,
                DefaultExt = SqlFileExtension,
                Multiselect = true,
                CheckFileExists = true
            };

            if (string.IsNullOrWhiteSpace(initialDirectory) == false && Directory.Exists(initialDirectory) == true)
            {
                dialog.InitialDirectory = initialDirectory;
            }

            bool? result = ShowCommonDialog(dialog);

            if (result == true)
            {
                return dialog.FileNames;
            }

            return Array.Empty<string>();
        }

        /// <inheritdoc />
        public string? ShowSaveFileDialog(string suggestedFileName, string? initialDirectory)
        {
            SaveFileDialog dialog = new()
            {
                Title = "Save File As",
                Filter = SqlFileFilter,
                DefaultExt = SqlFileExtension,
                AddExtension = true,
                OverwritePrompt = true,
                FileName = suggestedFileName
            };

            if (string.IsNullOrWhiteSpace(initialDirectory) == false && Directory.Exists(initialDirectory) == true)
            {
                dialog.InitialDirectory = initialDirectory;
            }

            bool? result = ShowCommonDialog(dialog);

            if (result == true)
            {
                return dialog.FileName;
            }

            return null;
        }

        /// <inheritdoc />
        public void ShowInformation(string message, string title)
        {
            Window? owner = GetOwner();

            if (owner is null)
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <inheritdoc />
        public void ShowError(string message, string title)
        {
            Window? owner = GetOwner();

            if (owner is null)
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <inheritdoc />
        public string? PromptForText(string title, string prompt, string initialValue = "", Func<string, bool>? validator = null)
        {
            InputDialog dialog = new(title, prompt, initialValue, validator)
            {
                Owner = GetOwner()
            };

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                return dialog.ResponseText;
            }

            return null;
        }

        /// <inheritdoc />
        public ConnectionProfile? ShowConnectionDialog()
        {
            ConnectionDialogViewModel viewModel = serviceProvider.GetRequiredService<ConnectionDialogViewModel>();

            ConnectionDialog dialog = new(viewModel)
            {
                Owner = GetOwner()
            };

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                return viewModel.Result;
            }

            return null;
        }

        /// <inheritdoc />
        public bool ShowOptionsDialog()
        {
            OptionsViewModel viewModel = serviceProvider.GetRequiredService<OptionsViewModel>();

            OptionsDialog dialog = new(viewModel)
            {
                Owner = GetOwner()
            };

            dialog.ShowDialog();

            return viewModel.Saved;
        }

        /// <inheritdoc />
        public bool ShowMetadataDialog(string? connectionName, string? database)
        {
            MetadataDialogViewModel viewModel = serviceProvider.GetRequiredService<MetadataDialogViewModel>();
            viewModel.Initialize(connectionName, database);

            MetadataDialog dialog = new(viewModel)
            {
                Owner = GetOwner()
            };

            dialog.ShowDialog();

            return viewModel.Saved;
        }

        /// <inheritdoc />
        public void ShowMetadataPromptDialog()
        {
            MetadataPromptDialogViewModel viewModel = serviceProvider.GetRequiredService<MetadataPromptDialogViewModel>();

            MetadataPromptDialog dialog = new(viewModel)
            {
                Owner = GetOwner()
            };

            dialog.ShowDialog();
        }

        /// <summary>
        /// Shows a Windows common dialog, owned by the main window when there is one.
        /// </summary>
        /// <param name="dialog">The dialog to show.</param>
        /// <returns>The dialog result.</returns>
        private static bool? ShowCommonDialog(CommonDialog dialog)
        {
            Window? owner = GetOwner();

            if (owner is null)
            {
                return dialog.ShowDialog();
            }

            return dialog.ShowDialog(owner);
        }

        /// <summary>
        /// Returns the current main window to use as the dialog owner, or null when
        /// none is available.
        /// </summary>
        /// <returns>The owner window, or null.</returns>
        private static Window? GetOwner()
        {
            if (Application.Current is null)
            {
                return null;
            }

            return Application.Current.MainWindow;
        }
    }
}
