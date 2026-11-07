using System.Windows;
using LightSQL.ViewModels;

namespace LightSQL.Views
{
    /// <summary>
    /// Shows the embedded metadata-extraction prompt with a Copy button.
    /// </summary>
    public partial class MetadataPromptDialog : Window
    {
        /// <summary>
        /// Creates the dialog for the given view model.
        /// </summary>
        /// <param name="viewModel">The prompt view model.</param>
        public MetadataPromptDialog(MetadataPromptDialogViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
        }
    }
}
