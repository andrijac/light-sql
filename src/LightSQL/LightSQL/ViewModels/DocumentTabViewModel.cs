using CommunityToolkit.Mvvm.ComponentModel;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// Base class of everything shown as a tab in the main document area: query
    /// tabs (<see cref="QueryTabViewModel"/>) and MCP server tabs
    /// (<see cref="McpServerTabViewModel"/>). The main window picks the header and
    /// content view of each tab by its concrete type.
    /// </summary>
    public abstract partial class DocumentTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected;

        /// <summary>
        /// The text shown in the tab header.
        /// </summary>
        public abstract string Header { get; }

        /// <summary>
        /// The tooltip of the tab header.
        /// </summary>
        public abstract string ToolTip { get; }

        /// <summary>
        /// The tab's name as used in messages (without markers such as " *").
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// Called once the tab has been removed from the tab strip, to release what
        /// it holds (cancel a running query, stop a server, ...).
        /// </summary>
        public abstract void OnClosed();
    }
}
