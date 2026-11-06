using System.Net;
using System.Net.Sockets;
using LightSQL.Services.Mcp;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests how <see cref="McpPortFinder"/> picks a free port for an MCP server.
    /// </summary>
    public class McpPortFinderTests
    {
        [Fact]
        public void FindAvailablePort_ReturnsFirstFreePort()
        {
            HashSet<int> taken = new() { 9000, 9001 };

            int? port = McpPortFinder.FindAvailablePort(9000, 9010, candidate => taken.Contains(candidate) == false);

            Assert.Equal(9002, port);
        }

        [Fact]
        public void FindAvailablePort_SkipsExcludedPorts()
        {
            HashSet<int> excluded = new() { 9000 };

            int? port = McpPortFinder.FindAvailablePort(9000, 9010, candidate => true, excluded);

            Assert.Equal(9001, port);
        }

        [Fact]
        public void FindAvailablePort_ReturnsNull_WhenEveryPortIsTaken()
        {
            int? port = McpPortFinder.FindAvailablePort(9000, 9003, candidate => false);

            Assert.Null(port);
        }

        [Fact]
        public void DefaultRange_StartsAt9000()
        {
            Assert.Equal(9000, McpPortFinder.FirstPort);
            Assert.True(McpPortFinder.LastPort > McpPortFinder.FirstPort);
        }

        [Fact]
        public void IsLoopbackPortAvailable_IsFalse_ForAPortInUse()
        {
            // Let the OS pick a free port and hold it.
            TcpListener holder = new(IPAddress.Loopback, 0);
            holder.Start();

            try
            {
                int port = ((IPEndPoint)holder.LocalEndpoint).Port;

                Assert.False(McpPortFinder.IsLoopbackPortAvailable(port));
            }
            finally
            {
                holder.Stop();
            }
        }

        [Fact]
        public void IsLoopbackPortAvailable_IsTrue_ForAFreePort()
        {
            TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            Assert.True(McpPortFinder.IsLoopbackPortAvailable(port));
        }
    }
}
