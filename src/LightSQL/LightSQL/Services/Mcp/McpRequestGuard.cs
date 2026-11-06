namespace LightSQL.Services.Mcp
{
    /// <summary>
    /// Decides whether an HTTP request may reach the MCP endpoint. The server only
    /// listens on 127.0.0.1, but a web page open in a browser could still try to
    /// call it (directly, or through DNS rebinding). Requests are therefore allowed
    /// only when the Host header names the loopback address and any Origin header
    /// is a local page, as the MCP specification recommends.
    /// </summary>
    public static class McpRequestGuard
    {
        /// <summary>
        /// Host names that refer to this machine.
        /// </summary>
        private static readonly string[] loopbackHosts = { "127.0.0.1", "localhost", "[::1]" };

        /// <summary>
        /// Whether the request's Host header refers to this machine.
        /// </summary>
        /// <param name="host">The Host header without the port, e.g. "127.0.0.1".</param>
        /// <returns>True for a loopback host name.</returns>
        public static bool IsAllowedHost(string? host)
        {
            if (string.IsNullOrWhiteSpace(host) == true)
            {
                return false;
            }

            return loopbackHosts.Contains(host, StringComparer.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Whether the request's Origin header is acceptable. AI clients such as
        /// Claude Code or Codex send no Origin; browsers always do, so only local
        /// pages are allowed.
        /// </summary>
        /// <param name="origin">The Origin header, or null when absent.</param>
        /// <returns>True when there is no origin or it is a loopback origin.</returns>
        public static bool IsAllowedOrigin(string? origin)
        {
            if (string.IsNullOrEmpty(origin) == true)
            {
                return true;
            }

            if (Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) == false)
            {
                return false;
            }

            bool isHttp = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

            return isHttp == true && uri.IsLoopback == true;
        }
    }
}
