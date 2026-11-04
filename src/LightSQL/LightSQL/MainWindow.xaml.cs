using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LightSQL.ViewModels;

namespace LightSQL
{
    /// <summary>
    /// The application's main window. Hosts the menu, toolbar, Object Explorer, the
    /// query tabs (each with its own editor and results) and the footer.
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
        }

        /// <summary>
        /// Asks to save modified query tabs before the window closes; cancels the
        /// close when the user chooses Cancel.
        /// </summary>
        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            if (viewModel.ConfirmCloseAll() == false)
            {
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Closes a query tab when its header is middle-clicked (as in SSMS and browsers).
        /// </summary>
        private void QueryTabStrip_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle)
            {
                return;
            }

            TabItem? tabItem = FindAncestor<TabItem>(e.OriginalSource as DependencyObject);

            if (tabItem?.DataContext is QueryTabViewModel tab)
            {
                viewModel.CloseQueryCommand.Execute(tab);
                e.Handled = true;
            }
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

        /// <summary>
        /// Walks up the visual tree to find the nearest ancestor of a given type.
        /// </summary>
        /// <typeparam name="T">The ancestor type to find.</typeparam>
        /// <param name="element">The element to start from (inclusive).</param>
        /// <returns>The ancestor, or null when there is none.</returns>
        private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
        {
            DependencyObject? current = element;

            while (current is not null)
            {
                if (current is T match)
                {
                    return match;
                }

                // Text runs are not visuals; fall back to the logical parent for them.
                current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
