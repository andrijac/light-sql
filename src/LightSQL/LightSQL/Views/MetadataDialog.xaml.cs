using System.Windows;
using LightSQL.ViewModels;

namespace LightSQL.Views
{
    /// <summary>
    /// The Database Metadata dialog, where the user pastes a JSON metadata document for
    /// a connection + database.
    /// </summary>
    public partial class MetadataDialog : Window
    {
        private readonly MetadataDialogViewModel viewModel;

        /// <summary>
        /// Creates the dialog for the given view model.
        /// </summary>
        /// <param name="viewModel">The metadata view model.</param>
        public MetadataDialog(MetadataDialogViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            DataContext = viewModel;
        }

        /// <summary>
        /// Validates and saves the metadata, then closes the dialog. Keeps the dialog
        /// open and reports the problem when saving fails.
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.TrySave(out string? error) == false)
            {
                MessageBox.Show(this, error, "Database Metadata", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            DialogResult = true;
        }
    }
}
