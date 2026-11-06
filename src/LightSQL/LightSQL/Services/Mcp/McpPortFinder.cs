using System.Net;
using System.Net.Sockets;

namespace LightSQL.Services.Mcp
{
    /// <summary>
    /// Finds a free TCP port for a new MCP server in the 9000+ range.
    /// </summary>
    public static class McpPortFinder
    {
        /// <summary>
        /// The first port tried for an MCP server.
        /// </summary>
        public const int FirstPort = 9000;

        /// <summary>
        /// The last port tried for an MCP server.
        /// </summary>
        public const int LastPort = 9999;

        /// <summary>
        /// Returns the first port in a range for which <paramref name="isAvailable"/>
        /// returns true, skipping ports already known to be taken.
        /// </summary>
        /// <param name="firstPort">The first port to try (inclusive).</param>
        /// <param name="lastPort">The last port to try (inclusive).</param>
        /// <param name="isAvailable">Checks whether a port can be bound.</param>
        /// <param name="excludedPorts">Ports to skip (for example ones that just failed to bind).</param>
        /// <returns>The free port, or null when every port in the range is taken.</returns>
        public static int? FindAvailablePort(int firstPort, int lastPort, Func<int, bool> isAvailable, IReadOnlySet<int>? excludedPorts = null)
        {
            for (int port = firstPort; port <= lastPort; port++)
            {
                if (excludedPorts is not null && excludedPorts.Contains(port) == true)
                {
                    continue;
                }

                if (isAvailable(port) == true)
                {
                    return port;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks whether a port can be bound on the loopback interface by briefly
        /// opening a listener on it.
        /// </summary>
        /// <param name="port">The port to check.</param>
        /// <returns>True when the port is free.</returns>
        public static bool IsLoopbackPortAvailable(int port)
        {
            TcpListener listener = new(IPAddress.Loopback, port);

            // Without this, Windows could let us bind a port another process holds
            // with SO_REUSEADDR, and the MCP server would then fail to receive.
            listener.ExclusiveAddressUse = true;

            try
            {
                listener.Start();

                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
