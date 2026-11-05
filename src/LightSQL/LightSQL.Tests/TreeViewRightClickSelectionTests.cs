using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LightSQL.Behaviors;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests that a right-click on a tree view node selects that node
    /// (<see cref="TreeViewRightClickSelection"/>). Runs real WPF controls in a window
    /// on an STA thread and raises the same mouse event a real click raises.
    /// </summary>
    public class TreeViewRightClickSelectionTests
    {
        [Fact]
        public void RightClick_OnNestedNode_SelectsThatNode_NotItsParent()
        {
            (bool childSelected, bool parentSelected, object? selectedItem, object child) = RunOnStaThread(() =>
            {
                using TestTree tree = new(isEnabled: true);

                RightClick(tree.ChildHeader);

                return (tree.Child.IsSelected, tree.Parent.IsSelected, tree.TreeView.SelectedItem, (object)tree.Child);
            });

            Assert.True(childSelected);
            Assert.False(parentSelected);
            Assert.Same(child, selectedItem);
        }

        [Fact]
        public void RightClick_MovesSelectionFromAnotherNode()
        {
            (bool parentSelected, bool childSelected) = RunOnStaThread(() =>
            {
                using TestTree tree = new(isEnabled: true);
                tree.Child.IsSelected = true;

                RightClick(tree.ParentHeader);

                return (tree.Parent.IsSelected, tree.Child.IsSelected);
            });

            Assert.True(parentSelected);
            Assert.False(childSelected);
        }

        [Fact]
        public void RightClick_WhenNotEnabled_DoesNotSelect()
        {
            bool childSelected = RunOnStaThread(() =>
            {
                using TestTree tree = new(isEnabled: false);

                RightClick(tree.ChildHeader);

                return tree.Child.IsSelected;
            });

            Assert.False(childSelected);
        }

        [Fact]
        public void SelectItemContaining_OutsideAnyItem_ReturnsNull()
        {
            bool nothingSelected = RunOnStaThread(() =>
            {
                using TestTree tree = new(isEnabled: true);

                TreeViewItem? item = TreeViewRightClickSelection.SelectItemContaining(tree.TreeView);

                return item is null && tree.TreeView.SelectedItem is null;
            });

            Assert.True(nothingSelected);
        }

        [Fact]
        public void SelectItemContaining_NullSource_ReturnsNull()
        {
            Assert.Null(TreeViewRightClickSelection.SelectItemContaining(null));
        }

        /// <summary>
        /// Raises a right mouse-button press on an element the way the input system
        /// does: a tunnelling PreviewMouseDown, which WPF turns into
        /// PreviewMouseRightButtonDown on every element along the route.
        /// </summary>
        /// <param name="element">The element under the mouse.</param>
        private static void RightClick(UIElement element)
        {
            MouseButtonEventArgs args = new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            {
                RoutedEvent = Mouse.PreviewMouseDownEvent,
                Source = element
            };

            element.RaiseEvent(args);
            Pump();
        }

        /// <summary>
        /// Lets the dispatcher process pending layout and binding work.
        /// </summary>
        private static void Pump()
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// Runs a function on a new STA thread (required for WPF controls) and returns
        /// its result, rethrowing any exception.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="function">The function to run.</param>
        /// <returns>The function's result.</returns>
        private static T RunOnStaThread<T>(Func<T> function)
        {
            T result = default!;
            Exception? failure = null;

            Thread thread = new(() =>
            {
                try
                {
                    result = function();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure is not null)
            {
                throw new InvalidOperationException("The STA test code failed.", failure);
            }

            return result;
        }

        /// <summary>
        /// A shown window with a tree of one expanded parent node and one child node,
        /// whose headers are text blocks (the element a user actually clicks).
        /// </summary>
        private sealed class TestTree : IDisposable
        {
            private readonly Window window;

            /// <summary>
            /// Builds and shows the tree.
            /// </summary>
            /// <param name="isEnabled">Whether to enable right-click selection.</param>
            public TestTree(bool isEnabled)
            {
                ParentHeader = new TextBlock { Text = "Databases" };
                ChildHeader = new TextBlock { Text = "Sales" };
                Child = new TreeViewItem { Header = ChildHeader };
                Parent = new TreeViewItem { Header = ParentHeader, IsExpanded = true };
                Parent.Items.Add(Child);

                TreeView = new TreeView();
                TreeView.Items.Add(Parent);
                TreeViewRightClickSelection.SetIsEnabled(TreeView, isEnabled);

                window = new Window
                {
                    Content = TreeView,
                    Width = 300,
                    Height = 200,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -10000,
                    Top = -10000
                };

                window.Show();
                Pump();
            }

            /// <summary>
            /// The tree view.
            /// </summary>
            public TreeView TreeView { get; }

            /// <summary>
            /// The top-level node.
            /// </summary>
            public TreeViewItem Parent { get; }

            /// <summary>
            /// The nested node.
            /// </summary>
            public TreeViewItem Child { get; }

            /// <summary>
            /// The top-level node's header text.
            /// </summary>
            public TextBlock ParentHeader { get; }

            /// <summary>
            /// The nested node's header text.
            /// </summary>
            public TextBlock ChildHeader { get; }

            /// <summary>
            /// Closes the window.
            /// </summary>
            public void Dispose()
            {
                window.Close();
            }
        }
    }
}
