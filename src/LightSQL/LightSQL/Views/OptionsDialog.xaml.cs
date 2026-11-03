using System.Windows;
using LightSQL.ViewModels;

namespace LightSQL.Views
{
    /// <summary>
    /// The Options dialog.
    /// </summary>
    public partial class OptionsDialog : Window
    {
        private readonly OptionsViewModel viewModel;

        /// <summary>
        /// Creates the dialog for the given view model.
        /// </summary>
        /// <param name="viewModel">The options view model.</param>
        public OptionsDialog(OptionsViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            DataContext = viewModel;
        }

        /// <summary>
        /// Persists the options and closes the dialog.
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Save();
            DialogResult = true;
        }
    }
}
