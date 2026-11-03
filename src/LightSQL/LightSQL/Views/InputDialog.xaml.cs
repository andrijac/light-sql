using System.Windows;
using System.Windows.Input;

namespace LightSQL.Views
{
    /// <summary>
    /// A small reusable dialog that prompts for a single line of text, with optional
    /// validation.
    /// </summary>
    public partial class InputDialog : Window
    {
        private readonly Func<string, bool>? validator;

        /// <summary>
        /// Creates the dialog.
        /// </summary>
        /// <param name="title">The window title.</param>
        /// <param name="prompt">The label shown above the input box.</param>
        /// <param name="initialValue">The initial text.</param>
        /// <param name="validator">Optional validator returning true for a valid value.</param>
        public InputDialog(string title, string prompt, string initialValue, Func<string, bool>? validator)
        {
            InitializeComponent();

            this.validator = validator;

            Title = title;
            PromptText.Text = prompt;
            ResponseBox.Text = initialValue;

            Loaded += (sender, args) =>
            {
                ResponseBox.Focus();
                ResponseBox.SelectAll();
            };
        }

        /// <summary>
        /// The text the user entered.
        /// </summary>
        public string ResponseText { get; private set; } = string.Empty;

        /// <summary>
        /// Validates and accepts the entered text.
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            TryAccept();
        }

        /// <summary>
        /// Accepts the input when the user presses Enter.
        /// </summary>
        private void ResponseBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryAccept();
            }
        }

        /// <summary>
        /// Applies validation and closes the dialog with a positive result when valid.
        /// </summary>
        private void TryAccept()
        {
            string value = ResponseBox.Text?.Trim() ?? string.Empty;

            if (validator is not null && validator(value) == false)
            {
                MessageBox.Show(this, "Please enter a valid value.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            ResponseText = value;
            DialogResult = true;
        }
    }
}
