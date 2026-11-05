using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LightSQL.Behaviors
{
    /// <summary>
    /// Attached behaviour that makes a right-click on a <see cref="TreeView"/> node
    /// select (and focus) that node before its context menu opens, as in SSMS and
    /// Windows Explorer. WPF's TreeView only selects on a left-click by default, so
    /// otherwise the context menu acts on one node while another stays highlighted.
    /// </summary>
    /// <example>
    /// <code>&lt;TreeView behaviors:TreeViewRightClickSelection.IsEnabled="True" /&gt;</code>
    /// </example>
    public static class TreeViewRightClickSelection
    {
        /// <summary>
        /// Identifies the <c>IsEnabled</c> attached property.
        /// </summary>
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(TreeViewRightClickSelection),
            new PropertyMetadata(false, OnIsEnabledChanged));

        /// <summary>
        /// Gets whether right-click selection is enabled on a tree view.
        /// </summary>
        /// <param name="element">The tree view.</param>
        /// <returns>True when enabled.</returns>
        public static bool GetIsEnabled(DependencyObject element)
        {
            return (bool)element.GetValue(IsEnabledProperty);
        }

        /// <summary>
        /// Sets whether right-click selection is enabled on a tree view.
        /// </summary>
        /// <param name="element">The tree view.</param>
        /// <param name="value">True to enable.</param>
        public static void SetIsEnabled(DependencyObject element, bool value)
        {
            element.SetValue(IsEnabledProperty, value);
        }

        /// <summary>
        /// Selects and focuses the tree view item that contains an element (for
        /// example the text or icon that was clicked).
        /// </summary>
        /// <param name="source">The element that was clicked.</param>
        /// <returns>The selected item, or null when the element is not inside an item.</returns>
        public static TreeViewItem? SelectItemContaining(DependencyObject? source)
        {
            TreeViewItem? item = FindItem(source);

            if (item is null)
            {
                return null;
            }

            item.IsSelected = true;
            item.Focus();

            return item;
        }

        /// <summary>
        /// Hooks or unhooks the mouse handler when the property changes.
        /// </summary>
        /// <param name="element">The element the property is set on.</param>
        /// <param name="e">The change details.</param>
        private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (element is not TreeView treeView)
            {
                return;
            }

            // Remove first so setting True twice never adds the handler twice.
            treeView.PreviewMouseRightButtonDown -= OnPreviewMouseRightButtonDown;

            if ((bool)e.NewValue == true)
            {
                treeView.PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
            }
        }

        /// <summary>
        /// Selects the node under the mouse. The event is not marked handled, so the
        /// context menu still opens when the button is released.
        /// </summary>
        /// <param name="sender">The tree view.</param>
        /// <param name="e">The mouse event.</param>
        private static void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectItemContaining(e.OriginalSource as DependencyObject);
        }

        /// <summary>
        /// Walks up from an element to the nearest tree view item, i.e. the node that
        /// was clicked rather than one of its parents.
        /// </summary>
        /// <param name="element">The element to start from (inclusive).</param>
        /// <returns>The item, or null when there is none.</returns>
        private static TreeViewItem? FindItem(DependencyObject? element)
        {
            DependencyObject? current = element;

            while (current is not null)
            {
                if (current is TreeViewItem item)
                {
                    return item;
                }

                // A click outside every node (empty area) must not select anything.
                if (current is TreeView)
                {
                    return null;
                }

                // Text runs are not visuals; fall back to the logical parent for them.
                current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
