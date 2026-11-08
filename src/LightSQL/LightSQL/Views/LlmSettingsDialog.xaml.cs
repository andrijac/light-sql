using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using LightSQL.Models;
using LightSQL.ViewModels;

namespace LightSQL.Views
{
    /// <summary>
    /// The AI Provider dialog. Each provider's API key is held in its own PasswordBox and read in
    /// code-behind at save time, so keys are never placed in bindable properties.
    /// </summary>
    public partial class LlmSettingsDialog : Window
    {
        private readonly LlmSettingsDialogViewModel viewModel;
        private readonly Dictionary<LlmProvider, string?> typedKeys = new();

        /// <summary>
        /// Creates the dialog for the given view model.
        /// </summary>
        /// <param name="viewModel">The settings view model.</param>
        public LlmSettingsDialog(LlmSettingsDialogViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            DataContext = viewModel;
        }

        /// <summary>
        /// Stashes the typed key for the row's provider as the user types, so it can be read at
        /// save time without binding the password to a property.
        /// </summary>
        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox box && box.DataContext is LlmProviderKeyRow row)
            {
                typedKeys[row.Provider] = box.Password;
            }
        }

        /// <summary>
        /// Validates and saves the settings, then closes the dialog. Keeps the dialog open and
        /// reports the problem when saving fails.
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.TrySave(typedKeys, out string? error) == false)
            {
                MessageBox.Show(this, error, "AI Provider", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            DialogResult = true;
        }
    }
}
