using LightSQL.Services.Mcp;

namespace LightSQL.Tests
{
    /// <summary>
    /// A pretend running MCP server for view-model tests.
    /// </summary>
    public sealed class FakeMcpServerHost : IMcpServerHost
    {
        /// <summary>
        /// Creates the fake server.
        /// </summary>
        /// <param name="port">The port it pretends to listen on.</param>
        public FakeMcpServerHost(int port)
        {
            Port = port;
        }

        /// <inheritdoc />
        public int Port { get; }

        /// <inheritdoc />
        public string Endpoint
        {
            get
            {
                return $"http://127.0.0.1:{Port}/mcp";
            }
        }

        /// <summary>
        /// How many times <see cref="StopAsync"/> was called.
        /// </summary>
        public int StopCount { get; private set; }

        /// <inheritdoc />
        public Task StopAsync()
        {
            StopCount++;

            return Task.CompletedTask;
        }
    }
}
