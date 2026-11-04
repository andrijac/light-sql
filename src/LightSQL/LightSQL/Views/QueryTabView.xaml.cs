using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LightSQL.Views
{
    /// <summary>
    /// The content of one query tab: the SQL editor and its results. The editor gets
    /// keyboard focus whenever the tab becomes visible (selected), like SSMS.
    /// </summary>
    public partial class QueryTabView : UserControl
    {
        /// <summary>
        /// Creates the view and wires focus handling.
        /// </summary>
        public QueryTabView()
        {
            InitializeComponent();

            IsVisibleChanged += OnIsVisibleChanged;
        }

        /// <summary>
        /// Focuses the editor when the tab is shown.
        /// </summary>
        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool visible && visible == true)
            {
                // Defer until layout has run; focusing a just-shown element is ignored otherwise.
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(SqlEditor.FocusEditor));
            }
        }
    }
}
