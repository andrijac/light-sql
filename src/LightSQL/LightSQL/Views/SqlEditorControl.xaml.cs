using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using LightSQL.Editor;
using LightSQL.Services;

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
        /// Backing property for whether line numbers are shown (bindable to options).
        /// </summary>
        public static readonly DependencyProperty ShowLineNumbersProperty = DependencyProperty.Register(
            nameof(ShowLineNumbers),
            typeof(bool),
            typeof(SqlEditorControl),
            new PropertyMetadata(true, OnShowLineNumbersChanged));

        /// <summary>
        /// Backing property for the editor font size in points (bindable to options).
        /// </summary>
        public static readonly DependencyProperty EditorFontSizeProperty = DependencyProperty.Register(
            nameof(EditorFontSize),
            typeof(double),
            typeof(SqlEditorControl),
            new PropertyMetadata(13d, OnEditorFontSizeChanged));

        /// <summary>
        /// Follows the palette's "Theme.Name" resource (set with SetResourceReference),
        /// so its change callback runs whenever the theme is switched. Used to
        /// re-apply the syntax colours, which are not dependency properties.
        /// </summary>
        private static readonly DependencyProperty ThemeNameProperty = DependencyProperty.Register(
            "ThemeName",
            typeof(object),
            typeof(SqlEditorControl),
            new PropertyMetadata(null, OnThemeNameChanged));

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

            // Selection colour follows the theme; selected text keeps its syntax colour.
            Editor.TextArea.SetResourceReference(TextArea.SelectionBrushProperty, "Theme.Editor.Selection");
            Editor.TextArea.SelectionForeground = null;
            Editor.TextArea.SelectionBorder = null;

            // Applies the syntax colours now and again on every theme switch.
            SetResourceReference(ThemeNameProperty, ThemeService.ThemeNameKey);

            if (Editor.SyntaxHighlighting is null)
            {
                // No theme resources found (e.g. outside the app): default colours.
                ApplyThemeColors();
            }

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
        /// Whether line numbers are shown.
        /// </summary>
        public bool ShowLineNumbers
        {
            get
            {
                return (bool)GetValue(ShowLineNumbersProperty);
            }

            set
            {
                SetValue(ShowLineNumbersProperty, value);
            }
        }

        /// <summary>
        /// The editor font size in points. Values of zero or less are ignored.
        /// </summary>
        public double EditorFontSize
        {
            get
            {
                return (double)GetValue(EditorFontSizeProperty);
            }

            set
            {
                SetValue(EditorFontSizeProperty, value);
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
        /// Applies the ShowLineNumbers DP to the editor.
        /// </summary>
        private static void OnShowLineNumbersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SqlEditorControl control = (SqlEditorControl)d;

            control.Editor.ShowLineNumbers = (bool)e.NewValue;
        }

        /// <summary>
        /// Applies the EditorFontSize DP to the editor, ignoring invalid sizes.
        /// </summary>
        private static void OnEditorFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SqlEditorControl control = (SqlEditorControl)d;
            double fontSize = (double)e.NewValue;

            if (fontSize > 0d)
            {
                control.Editor.FontSize = fontSize;
            }
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
        /// Re-applies the syntax colours when the theme changes.
        /// </summary>
        private static void OnThemeNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SqlEditorControl control = (SqlEditorControl)d;

            control.ApplyThemeColors();
        }

        /// <summary>
        /// Loads the T-SQL highlighting with the current theme's syntax colours and
        /// sets the caret colour (neither can use DynamicResource).
        /// </summary>
        private void ApplyThemeColors()
        {
            Dictionary<string, Color> colors = new();

            foreach (string name in SqlHighlighting.GetColorNames())
            {
                if (TryFindResource(SqlHighlighting.ResourceKeyPrefix + name) is SolidColorBrush brush)
                {
                    colors[name] = brush.Color;
                }
            }

            IHighlightingDefinition? definition = SqlHighlighting.Create(colors);

            if (definition is not null)
            {
                Editor.SyntaxHighlighting = definition;
            }

            if (TryFindResource("Theme.Editor.Foreground") is Brush caretBrush)
            {
                Editor.TextArea.Caret.CaretBrush = caretBrush;
            }
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
