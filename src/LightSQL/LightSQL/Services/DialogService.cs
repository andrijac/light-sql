using System.Windows;
using LightSQL.Models;
using LightSQL.ViewModels;
using LightSQL.Views;
using Microsoft.Extensions.DependencyInjection;

namespace LightSQL.Services
{
    /// <summary>
    /// WPF implementation of <see cref="IDialogService"/> using standard message
    /// boxes, the reusable <see cref="InputDialog"/>, and the application's modal
    /// dialog windows resolved from the service provider.
    /// </summary>
    public sealed class DialogService : IDialogService
    {
        private readonly IServiceProvider serviceProvider;

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
