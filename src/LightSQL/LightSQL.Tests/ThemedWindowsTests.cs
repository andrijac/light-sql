using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.ViewModels;
using LightSQL.Views;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Shows the real windows off-screen under every theme and switches themes while
    /// they are open. Catches broken control templates (they only fail when applied)
    /// and checks that colours follow the palette at runtime.
    /// </summary>
    public class ThemedWindowsTests
    {
        [Fact]
        public void MainWindow_FollowsEveryThemeAtRuntime()
        {
            List<string> problems = StaThread.Run(() =>
            {
                List<string> found = new();

                MainWindow window = new(CreateMainViewModel());
                ThemeService themeService = AttachThemes(window);

                ShowOffScreen(window);

                foreach (AppTheme theme in new[] { AppTheme.Light, AppTheme.Dark, AppTheme.Classic, AppTheme.Gradient })
                {
                    themeService.ApplyTheme(theme);
                    DoEvents();

                    // Open a menu and the toolbar dropdown so their popup templates are applied too.
                    MenuItem fileMenu = FindVisualChildren<MenuItem>(window).First();
                    fileMenu.IsSubmenuOpen = true;
                    ComboBox databases = FindVisualChildren<ComboBox>(window).First();
                    databases.IsDropDownOpen = true;
                    DoEvents();
                    fileMenu.IsSubmenuOpen = false;
                    databases.IsDropDownOpen = false;

                    CheckBrush(found, theme, "window", window.Background, window, "Theme.WindowBackground");
                    CheckBrush(found, theme, "tree", FindVisualChildren<TreeView>(window).First().Background, window, "Theme.PanelBackground");
                    CheckBrush(found, theme, "status bar", FindVisualChildren<StatusBar>(window).First().Background, window, "Theme.StatusBarBackground");

                    TextEditor editor = FindVisualChildren<TextEditor>(window).First();
                    CheckBrush(found, theme, "editor", editor.Background, window, "Theme.Editor.Background");

                    // Syntax colours are applied in code when the palette changes.
                    Color keyword = ((SolidColorBrush)window.FindResource("Theme.Syntax.Keyword")).Color;

                    if (editor.SyntaxHighlighting.GetNamedColor("Keyword").Foreground.GetColor(null!) != keyword)
                    {
                        found.Add($"{theme}: editor keyword colour not updated");
                    }
                }

                window.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();

                return found;
            });

            Assert.Empty(problems);
        }

        [Fact]
        public void MainWindow_ShowsMcpServerTab_UnderEveryTheme()
        {
            List<string> problems = StaThread.Run(() =>
            {
                List<string> found = new();

                FakeDialogService dialogService = new() { ConnectionProfileResult = new ConnectionProfile { Server = "fake-server" } };
                MainViewModel viewModel = CreateMainViewModel(dialogService);

                // The fakes complete synchronously, so waiting here cannot deadlock.
                viewModel.ConnectCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                viewModel.OpenMcpServerTabAsync("Sales").GetAwaiter().GetResult();

                MainWindow window = new(viewModel);
                ThemeService themeService = AttachThemes(window);

                ShowOffScreen(window);

                foreach (AppTheme theme in new[] { AppTheme.Light, AppTheme.Dark, AppTheme.Classic, AppTheme.Gradient })
                {
                    themeService.ApplyTheme(theme);
                    DoEvents();

                    McpServerTabView? view = FindVisualChildren<McpServerTabView>(window).FirstOrDefault();

                    if (view is null || view.IsVisible == false)
                    {
                        found.Add($"{theme}: MCP server view not shown");

                        continue;
                    }

                    if (FindVisualChildren<TextBox>(view).Any(box => box.Text == "http://127.0.0.1:9000/mcp") == false)
                    {
                        found.Add($"{theme}: endpoint not shown");
                    }

                    // The query editor of the other tab is hidden while the MCP tab is selected.
                    if (FindVisualChildren<QueryTabView>(window).Any(queryView => queryView.IsVisible == true) == true)
                    {
                        found.Add($"{theme}: query view visible behind the MCP tab");
                    }

                    TabItem mcpTabItem = FindVisualChildren<TabItem>(window).Single(item => item.DataContext is McpServerTabViewModel);

                    if (mcpTabItem.ContextMenu?.Items.OfType<MenuItem>().Any(item => (string)item.Header == "Stop Server") != true)
                    {
                        found.Add($"{theme}: MCP tab has no server context menu");
                    }
                }

                window.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();

                return found;
            });

            Assert.Empty(problems);
        }

        [Fact]
        public void Dialogs_RenderUnderEveryTheme()
        {
            StaThread.Run(() =>
            {
                List<Window> dialogs = new()
                {
                    new OptionsDialog(new OptionsViewModel(new FakeOptionsService(), new FakeThemeService())),
                    new ConnectionDialog(new ConnectionDialogViewModel(new FakeSqlConnectionService(), new FakeConnectionStore())),
                    new InputDialog("Title", "Prompt", "value", null)
                };

                foreach (Window dialog in dialogs)
                {
                    ThemeService themeService = AttachThemes(dialog);

                    ShowOffScreen(dialog);

                    foreach (AppTheme theme in new[] { AppTheme.Light, AppTheme.Dark, AppTheme.Classic, AppTheme.Gradient })
                    {
                        themeService.ApplyTheme(theme);
                        DoEvents();

                        Assert.Same(dialog.FindResource("Theme.WindowBackground"), dialog.Background);
                    }

                    dialog.Close();
                }

                Dispatcher.CurrentDispatcher.InvokeShutdown();
            });
        }

        /// <summary>
        /// The scroll bar thumb brush and the thumb style must not share a resource key.
        /// When they did, the thumb Border's dynamic background lookup resolved to the
        /// style, and the scroll bar crashed on render with
        /// "'System.Windows.Style' is not a valid value for property 'Background'".
        /// </summary>
        [Fact]
        public void ScrollBarThumbBackground_ResolvesToBrush_NotStyle()
        {
            object thumbBackground = StaThread.Run(() =>
            {
                // Merge the dictionaries in the same order as App.xaml: palette first,
                // themed control styles second. The later dictionary wins on a key
                // clash, so a style keyed the same as the brush would be returned here.
                ResourceDictionary resources = new();

                resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
                    new Uri("/LightSQL;component/Themes/Palettes/Light.xaml", UriKind.Relative)));
                resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
                    new Uri("/LightSQL;component/Themes/Controls.xaml", UriKind.Relative)));

                return resources["Theme.ScrollBarThumb"];
            });

            Assert.IsAssignableFrom<Brush>(thumbBackground);
        }

        /// <summary>
        /// Gives a window the theme resources (normally in App.xaml) and returns a
        /// theme service that swaps the palette in them.
        /// </summary>
        /// <param name="window">The window.</param>
        /// <returns>The theme service for that window.</returns>
        private static ThemeService AttachThemes(Window window)
        {
            window.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
                new Uri("/LightSQL;component/Themes/Controls.xaml", UriKind.Relative)));
            window.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
                new Uri("/LightSQL;component/Resources/ObjectExplorerIcons.xaml", UriKind.Relative)));

            ThemeService themeService = new(new FakeSystemThemeProvider(), window.Resources);
            themeService.ApplyTheme(AppTheme.Light);

            return themeService;
        }

        /// <summary>
        /// Shows a window outside the visible screen without activating it.
        /// </summary>
        /// <param name="window">The window.</param>
        private static void ShowOffScreen(Window window)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;
            window.Top = -20000;
            window.ShowInTaskbar = false;
            window.ShowActivated = false;
            window.Show();
            DoEvents();
        }

        /// <summary>
        /// Records a problem when an element's brush is not the palette's brush.
        /// </summary>
        private static void CheckBrush(List<string> problems, AppTheme theme, string element, Brush actual, FrameworkElement scope, string key)
        {
            if (ReferenceEquals(actual, scope.FindResource(key)) == false)
            {
                problems.Add($"{theme}: {element} does not use {key}");
            }
        }

        /// <summary>
        /// Processes pending dispatcher work (layout, rendering, bindings).
        /// </summary>
        private static void DoEvents()
        {
            DispatcherFrame frame = new();

            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        /// <summary>
        /// Enumerates the visual descendants of a given type.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="root">Where to start.</param>
        /// <returns>The matching descendants, depth first.</returns>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
            where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);

                if (child is T match)
                {
                    yield return match;
                }

                foreach (T descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Creates a main view model wired to the test fakes.
        /// </summary>
        /// <returns>The view model (one empty query tab).</returns>
        private static MainViewModel CreateMainViewModel()
        {
            return CreateMainViewModel(new FakeDialogService());
        }

        /// <summary>
        /// Creates a main view model wired to the test fakes, with a given dialog service
        /// (e.g. one that returns a connection).
        /// </summary>
        /// <param name="dialogService">The dialog service fake.</param>
        /// <returns>The view model (one empty query tab).</returns>
        private static MainViewModel CreateMainViewModel(FakeDialogService dialogService)
        {
            FakeSqlConnectionService sqlService = new();

            return new MainViewModel(
                sqlService,
                new SqlStatementAnalyzer(),
                new FakeFileService(),
                new FakeOptionsService(),
                dialogService,
                new ObjectExplorerViewModel(sqlService, dialogService),
                new FakeMcpServerHostFactory(),
                new FakeClipboardService());
        }
    }
}
