using System.Windows.Controls;

namespace LightSQL.Views
{
    /// <summary>
    /// The content of an MCP server tab: server details, AI client setup and the
    /// tool-call activity log. All behaviour lives in
    /// <see cref="ViewModels.McpServerTabViewModel"/>.
    /// </summary>
    public partial class McpServerTabView : UserControl
    {
        /// <summary>
        /// Creates the view.
        /// </summary>
        public McpServerTabView()
        {
            InitializeComponent();
        }
    }
}
