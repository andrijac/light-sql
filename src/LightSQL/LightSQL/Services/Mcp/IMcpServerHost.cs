namespace LightSQL.Services.Mcp
{
    /// <summary>
    /// A running MCP server: an HTTP endpoint on the loopback interface that AI
    /// clients connect to with the "Streamable HTTP" transport.
    /// </summary>
    public interface IMcpServerHost
    {
        /// <summary>
        /// The TCP port the server listens on (9000 or higher).
        /// </summary>
        int Port { get; }

        /// <summary>
        /// The URL AI clients connect to, e.g. "http://127.0.0.1:9000/mcp".
        /// </summary>
        string Endpoint { get; }

        /// <summary>
        /// Stops listening and releases the port. Safe to call more than once.
        /// </summary>
        /// <returns>A task that completes when the server has stopped.</returns>
        Task StopAsync();
    }
}
