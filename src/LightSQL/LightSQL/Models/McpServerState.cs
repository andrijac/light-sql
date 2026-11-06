namespace LightSQL.Models
{
    /// <summary>
    /// The lifecycle state of an MCP server started for a connection. Values start
    /// at 1 so an unset (default) value is never mistaken for a real state.
    /// </summary>
    public enum McpServerState
    {
        /// <summary>
        /// The server is connecting to the database and binding its port.
        /// </summary>
        Starting = 1,

        /// <summary>
        /// The server is listening and AI clients can call its tools.
        /// </summary>
        Running = 2,

        /// <summary>
        /// The server was stopped by the user.
        /// </summary>
        Stopped = 3,

        /// <summary>
        /// The server could not start (see the error message).
        /// </summary>
        Failed = 4
    }
}
