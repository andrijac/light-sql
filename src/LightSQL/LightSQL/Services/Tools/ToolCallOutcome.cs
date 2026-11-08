namespace LightSQL.Services.Tools
{
    /// <summary>
    /// The result of running a database tool: the text returned to the caller (JSON or an
    /// error message), whether it is an error, and a short outcome for the activity log.
    /// </summary>
    public sealed class ToolCallOutcome
    {
        /// <summary>
        /// The text returned to the caller (JSON for a success, a message for an error).
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// Whether the call failed.
        /// </summary>
        public bool IsError { get; init; }

        /// <summary>
        /// A short description of the outcome for the activity log.
        /// </summary>
        public string Outcome { get; init; } = string.Empty;
    }
}
