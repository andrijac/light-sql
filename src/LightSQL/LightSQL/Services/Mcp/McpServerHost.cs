using Microsoft.AspNetCore.Builder;

namespace LightSQL.Services.Mcp
{
    /// <summary>
    /// A running MCP server hosted in an in-process ASP.NET Core (Kestrel) web
    /// application.
    /// </summary>
    public sealed class McpServerHost : IMcpServerHost
    {
        private readonly WebApplication application;

        /// <summary>
        /// Set once stopping has begun, so the application is only stopped once.
        /// </summary>
        private int stopRequested;

        /// <summary>
        /// Wraps a started web application.
        /// </summary>
        /// <param name="application">The started application.</param>
        /// <param name="port">The port it listens on.</param>
        public McpServerHost(WebApplication application, int port)
        {
            this.application = application;

            Port = port;
            Endpoint = $"http://127.0.0.1:{port}{McpServerHostFactory.EndpointPath}";
        }

        /// <inheritdoc />
        public int Port { get; }

        /// <inheritdoc />
        public string Endpoint { get; }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref stopRequested, 1) == 1)
            {
                return;
            }

            // Give running tool calls a moment to finish, then drop the connections.
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));

            try
            {
                await application.StopAsync(timeout.Token).ConfigureAwait(false);
            }
            finally
            {
                await application.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
