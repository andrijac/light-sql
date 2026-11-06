using LightSQL.Models;
using LightSQL.Services.Mcp;

namespace LightSQL.Tests
{
    /// <summary>
    /// <see cref="IMcpServerHostFactory"/> for view-model tests: hands out fake
    /// servers on ports 9000, 9001, ... and records the settings it was given.
    /// </summary>
    public sealed class FakeMcpServerHostFactory : IMcpServerHostFactory
    {
        /// <summary>
        /// The port the next fake server gets.
        /// </summary>
        public int NextPort { get; set; } = McpPortFinder.FirstPort;

        /// <summary>
        /// When set, <see cref="StartAsync"/> throws this exception.
        /// </summary>
        public Exception? StartFailure { get; set; }

        /// <summary>
        /// The settings of every start, in order.
        /// </summary>
        public List<McpServerSettings> StartedSettings { get; } = new();

        /// <summary>
        /// Every fake server started, in order.
        /// </summary>
        public List<FakeMcpServerHost> Hosts { get; } = new();

        /// <summary>
        /// When set, <see cref="StartAsync"/> waits for this task before starting,
        /// so a test can act while a server is still starting.
        /// </summary>
        public TaskCompletionSource? StartGate { get; set; }

        /// <inheritdoc />
        public async Task<IMcpServerHost> StartAsync(McpServerSettings settings, CancellationToken cancellationToken)
        {
            StartedSettings.Add(settings);

            if (StartGate is not null)
            {
                await StartGate.Task;
            }

            if (StartFailure is not null)
            {
                throw StartFailure;
            }

            FakeMcpServerHost host = new(NextPort);
            NextPort++;
            Hosts.Add(host);

            return host;
        }
    }
}
