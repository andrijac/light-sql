using System.Windows;
using LightSQL.Models;
using LightSQL.ViewModels;

namespace LightSQL
{
    /// <summary>
    /// The application's main window. Hosts the menu, toolbar, Object Explorer,
    /// SQL editor, results and footer, and applies editor-related options.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel viewModel;

        /// <summary>
        /// Creates the main window.
        /// </summary>
        /// <param name="viewModel">The main view model (injected).</param>
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            DataContext = viewModel;

            viewModel.OptionsApplied += ApplyEditorOptions;

            Loaded += OnLoaded;
        }

        /// <summary>
        /// Applies editor options and focuses the editor once the window is shown.
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyEditorOptions();
            SqlEditor.FocusEditor();
        }

        /// <summary>
        /// Applies the current editor options (line numbers, font size) to the editor.
        /// </summary>
        private void ApplyEditorOptions()
        {
            AppOptions options = viewModel.Options;

            SqlEditor.SetShowLineNumbers(options.ShowLineNumbers);
            SqlEditor.SetFontSize(options.EditorFontSize);
        }

        /// <summary>
        /// Closes the application from the File menu.
        /// </summary>
        private void ExitMenu_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Shows the About dialog.
        /// </summary>
        private void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                this,
                "LightSQL\nA lightweight SQL Server query tool.\nBuilt with WPF on .NET 10.",
                "About LightSQL",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
