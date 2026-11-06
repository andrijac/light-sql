namespace LightSQL.Models
{
    /// <summary>
    /// One tool call an AI client made to an MCP server, shown in the server tab's
    /// activity log.
    /// </summary>
    public sealed class McpToolCall
    {
        /// <summary>
        /// Longest <see cref="DetailPreview"/>, in characters.
        /// </summary>
        public const int MaxPreviewLength = 200;

        /// <summary>
        /// When the call started (local time).
        /// </summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// The tool that was called, e.g. "execute_query".
        /// </summary>
        public string ToolName { get; init; } = string.Empty;

        /// <summary>
        /// The main argument of the call (the SQL text, the database or table name).
        /// </summary>
        public string Detail { get; init; } = string.Empty;

        /// <summary>
        /// The database the call ran against, or null for server-level tools.
        /// </summary>
        public string? Database { get; init; }

        /// <summary>
        /// Whether the call succeeded.
        /// </summary>
        public bool IsSuccess { get; init; }

        /// <summary>
        /// A short outcome, e.g. "3 result set(s), 120 row(s)" or the error message.
        /// </summary>
        public string Outcome { get; init; } = string.Empty;

        /// <summary>
        /// How long the call took.
        /// </summary>
        public TimeSpan Elapsed { get; init; }

        /// <summary>
        /// <see cref="Detail"/> on a single line (whitespace collapsed, cut at
        /// <see cref="MaxPreviewLength"/> characters) for the log grid; the full text
        /// is shown in a tooltip.
        /// </summary>
        public string DetailPreview
        {
            get
            {
                string singleLine = string.Join(' ', Detail.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

                return singleLine.Length <= MaxPreviewLength ? singleLine : singleLine.Substring(0, MaxPreviewLength) + "...";
            }
        }

        /// <summary>
        /// The time of the call formatted for the log, e.g. "14:03:27".
        /// </summary>
        public string TimeText
        {
            get
            {
                return Timestamp.ToString("HH:mm:ss");
            }
        }

        /// <summary>
        /// The duration formatted for the log, e.g. "35 ms".
        /// </summary>
        public string ElapsedText
        {
            get
            {
                return $"{Elapsed.TotalMilliseconds:N0} ms";
            }
        }
    }
}
