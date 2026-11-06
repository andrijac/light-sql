using System.Windows;
using System.Windows.Controls;
using LightSQL.ViewModels;

namespace LightSQL.Controls
{
    /// <summary>
    /// Picks the tab header style (and so the right-click menu) for each kind of
    /// document tab: query tabs offer Save / Save As, MCP server tabs offer
    /// Start / Stop. A single style could not bind both menus, because the
    /// commands take a specific tab type.
    /// </summary>
    public sealed class DocumentTabStyleSelector : StyleSelector
    {
        /// <summary>
        /// The style for <see cref="QueryTabViewModel"/> tabs.
        /// </summary>
        public Style? QueryTabStyle { get; set; }

        /// <summary>
        /// The style for <see cref="McpServerTabViewModel"/> tabs.
        /// </summary>
        public Style? McpServerTabStyle { get; set; }

        /// <summary>
        /// Returns the style for a tab's view model.
        /// </summary>
        /// <param name="item">The tab view model.</param>
        /// <param name="container">The TabItem.</param>
        /// <returns>The matching style, or the default selection when unknown.</returns>
        public override Style? SelectStyle(object item, DependencyObject container)
        {
            if (item is McpServerTabViewModel)
            {
                return McpServerTabStyle;
            }

            if (item is QueryTabViewModel)
            {
                return QueryTabStyle;
            }

            return base.SelectStyle(item, container);
        }
    }
}
