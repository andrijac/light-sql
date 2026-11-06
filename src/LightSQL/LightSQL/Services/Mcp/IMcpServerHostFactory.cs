using LightSQL.Models;

namespace LightSQL.Services.Mcp
{
    /// <summary>
    /// Starts MCP servers. Each server gets its own database connection and the
    /// first free port from 9000 up.
    /// </summary>
    public interface IMcpServerHostFactory
    {
        /// <summary>
        /// Connects to the database described by the settings and starts an MCP
        /// server for it.
        /// </summary>
        /// <param name="settings">The connection, default database and activity callback.</param>
        /// <param name="cancellationToken">Token to cancel the start.</param>
        /// <returns>The running server.</returns>
        Task<IMcpServerHost> StartAsync(McpServerSettings settings, CancellationToken cancellationToken);
    }
}
