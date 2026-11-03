using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace LightSQL.Views
{
    /// <summary>
    /// A SQL text editor built on AvalonEdit, exposing bindable document text,
    /// selection and caret position so it can be used from an MVVM view model.
    /// </summary>
    public partial class SqlEditorControl : UserControl
    {
        /// <summary>
        /// Backing property for the full document text (two-way bindable).
        /// </summary>
        public static readonly DependencyProperty DocumentTextProperty = DependencyProperty.Register(
            nameof(DocumentText),
            typeof(string),
            typeof(SqlEditorControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDocumentTextChanged));

        /// <summary>
        /// Backing property for the currently selected text (updated from the editor).
        /// </summary>
        public static readonly DependencyProperty SelectedTextProperty = DependencyProperty.Register(
            nameof(SelectedText),
            typeof(string),
            typeof(SqlEditorControl),
            new PropertyMetadata(string.Empty));

        /// <summary>
        /// Backing property for the caret line number (1-based).
        /// </summary>
        public static readonly DependencyProperty CaretLineProperty = DependencyProperty.Register(
            nameof(CaretLine),
            typeof(int),
            typeof(SqlEditorControl),
            new PropertyMetadata(1));

        /// <summary>
        /// Backing property for the caret column number (1-based).
        /// </summary>
        public static readonly DependencyProperty CaretColumnProperty = DependencyProperty.Register(
            nameof(CaretColumn),
            typeof(int),
            typeof(SqlEditorControl),
            new PropertyMetadata(1));

        /// <summary>
        /// Guards against feedback loops while syncing the editor and the DP.
        /// </summary>
        private bool isUpdatingDocumentText;

        /// <summary>
        /// Creates the control, loads SQL highlighting and wires editor events.
        /// </summary>
        public SqlEditorControl()
        {
            InitializeComponent();

            LoadSqlHighlighting();

            Editor.TextChanged += OnEditorTextChanged;
            Editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
            Editor.TextArea.SelectionChanged += OnSelectionChanged;
        }

        /// <summary>
        /// The full text of the editor document.
        /// </summary>
        public string DocumentText
        {
            get
            {
                return (string)GetValue(DocumentTextProperty);
            }

            set
            {
                SetValue(DocumentTextProperty, value);
            }
        }

        /// <summary>
        /// The currently selected text, or an empty string when nothing is selected.
        /// </summary>
        public string SelectedText
        {
            get
            {
                return (string)GetValue(SelectedTextProperty);
            }

            set
            {
                SetValue(SelectedTextProperty, value);
            }
        }

        /// <summary>
        /// The caret line number (1-based).
        /// </summary>
        public int CaretLine
        {
            get
            {
                return (int)GetValue(CaretLineProperty);
            }

            set
            {
                SetValue(CaretLineProperty, value);
            }
        }

        /// <summary>
        /// The caret column number (1-based).
        /// </summary>
        public int CaretColumn
        {
            get
            {
                return (int)GetValue(CaretColumnProperty);
            }

            set
            {
                SetValue(CaretColumnProperty, value);
            }
        }

        /// <summary>
        /// Sets whether line numbers are shown.
        /// </summary>
        /// <param name="show">True to show line numbers.</param>
        public void SetShowLineNumbers(bool show)
        {
            Editor.ShowLineNumbers = show;
        }

        /// <summary>
        /// Sets the editor font size in points.
        /// </summary>
        /// <param name="fontSize">The font size.</param>
        public void SetFontSize(double fontSize)
        {
            if (fontSize > 0d)
            {
                Editor.FontSize = fontSize;
            }
        }

        /// <summary>
        /// Gives keyboard focus to the editor.
        /// </summary>
        public void FocusEditor()
        {
            Editor.Focus();
        }

        /// <summary>
        /// Pushes the DP value into the editor when it changes from outside.
        /// </summary>
        private static void OnDocumentTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SqlEditorControl control = (SqlEditorControl)d;

            if (control.isUpdatingDocumentText == true)
            {
                return;
            }

            string newText = e.NewValue as string ?? string.Empty;

            if (control.Editor.Text != newText)
            {
                control.Editor.Text = newText;
            }
        }

        /// <summary>
        /// Loads the embedded T-SQL highlighting definition into the editor.
        /// </summary>
        private void LoadSqlHighlighting()
        {
            Assembly assembly = typeof(SqlEditorControl).Assembly;
            string resourceName = "LightSQL.Editor.SqlSyntax.xshd";

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);

            if (stream is null)
            {
                return;
            }

            using XmlReader reader = XmlReader.Create(stream);
            IHighlightingDefinition definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);

            Editor.SyntaxHighlighting = definition;
        }

        /// <summary>
        /// Mirrors editor text changes back into the DP.
        /// </summary>
        private void OnEditorTextChanged(object? sender, EventArgs e)
        {
            isUpdatingDocumentText = true;

            try
            {
                DocumentText = Editor.Text;
            }
            finally
            {
                isUpdatingDocumentText = false;
            }
        }

        /// <summary>
        /// Updates the caret line/column DPs when the caret moves.
        /// </summary>
        private void OnCaretPositionChanged(object? sender, EventArgs e)
        {
            CaretLine = Editor.TextArea.Caret.Line;
            CaretColumn = Editor.TextArea.Caret.Column;
        }

        /// <summary>
        /// Keeps the SelectedText DP in sync with the editor selection.
        /// </summary>
        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            SelectedText = Editor.TextArea.Selection.GetText();
        }
    }
}
