using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LightSQL.ViewModels;

namespace LightSQL.Views
{
    /// <summary>
    /// The connection dialog. Manages the two <see cref="PasswordBox"/> controls in
    /// code-behind so plaintext passwords are never bound.
    /// </summary>
    public partial class ConnectionDialog : Window
    {
        private readonly ConnectionDialogViewModel viewModel;

        /// <summary>
        /// Creates the dialog for the given view model.
        /// </summary>
        /// <param name="viewModel">The connection dialog view model.</param>
        public ConnectionDialog(ConnectionDialogViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            DataContext = viewModel;

            // Selecting a saved connection loads it in the view model; the password
            // boxes are not bound, so they are refreshed here.
            viewModel.SavedConnectionLoaded += OnSavedConnectionLoaded;

            // Pre-fill the password box for a loaded saved connection.
            if (string.IsNullOrEmpty(viewModel.Password) == false)
            {
                SyncPasswordBoxes();
            }
        }

        /// <summary>
        /// Detaches from the view model when the dialog closes.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected override void OnClosed(EventArgs e)
        {
            viewModel.SavedConnectionLoaded -= OnSavedConnectionLoaded;

            base.OnClosed(e);
        }

        /// <summary>
        /// Copies the loaded connection's password into the password boxes.
        /// </summary>
        private void OnSavedConnectionLoaded(object? sender, EventArgs e)
        {
            SyncPasswordBoxes();
        }

        /// <summary>
        /// Connects with a saved connection when its list entry is double-clicked.
        /// </summary>
        private void SavedConnectionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Ignore double-clicks on empty space or the scrollbar.
            if (ItemsControl.ContainerFromElement(SavedConnectionsList, e.OriginalSource as DependencyObject) is not ListBoxItem)
            {
                return;
            }

            TryConnect();
        }

        /// <summary>
        /// Captures the password and tests the connection.
        /// </summary>
        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            CapturePassword();
            viewModel.TestCommand.Execute(null);
        }

        /// <summary>
        /// Captures the password, validates, and closes the dialog on success.
        /// </summary>
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            TryConnect();
        }

        /// <summary>
        /// Captures the password and closes the dialog when the details are valid.
        /// </summary>
        private void TryConnect()
        {
            CapturePassword();

            if (viewModel.Accept() == true)
            {
                DialogResult = true;
            }
        }

        /// <summary>
        /// Copies the view model's password into both password boxes.
        /// </summary>
        private void SyncPasswordBoxes()
        {
            PasswordBox.Password = viewModel.Password ?? string.Empty;
            RawPasswordBox.Password = viewModel.Password ?? string.Empty;
        }

        /// <summary>
        /// Copies the password from the active password box into the view model.
        /// </summary>
        private void CapturePassword()
        {
            viewModel.Password = viewModel.UseRawConnectionString == true
                ? RawPasswordBox.Password
                : PasswordBox.Password;
        }
    }
}