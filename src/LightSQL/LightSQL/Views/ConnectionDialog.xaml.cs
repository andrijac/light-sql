using System.Windows;
using LightSQL.ViewModels;

namespace LightSQL.Views
{
    /// <summary>
    /// The connection dialog. Manages the two <see cref="System.Windows.Controls.PasswordBox"/>
    /// controls in code-behind so plaintext passwords are never bound.
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

            // Pre-fill the password box for a loaded saved connection.
            if (string.IsNullOrEmpty(viewModel.Password) == false)
            {
                PasswordBox.Password = viewModel.Password;
                RawPasswordBox.Password = viewModel.Password;
            }
        }

        /// <summary>
        /// Loads the selected saved connection and syncs the password box.
        /// </summary>
        private void UseButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.LoadSelected();

            PasswordBox.Password = viewModel.Password ?? string.Empty;
            RawPasswordBox.Password = viewModel.Password ?? string.Empty;
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
            CapturePassword();

            if (viewModel.Accept() == true)
            {
                DialogResult = true;
            }
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
