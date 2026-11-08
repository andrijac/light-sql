namespace LightSQL.Models
{
    /// <summary>
    /// One tool call the assistant made, shown inside a <see cref="ChatToolGroup"/>.
    /// </summary>
    public sealed class ChatToolCall
    {
        /// <summary>
        /// The tool's name (used as the group's collapsed title when it is the latest call).
        /// </summary>
        public required string ToolName { get; init; }

        /// <summary>
        /// The line shown when the group is expanded (the tool name and a short outcome).
        /// </summary>
        public required string Detail { get; init; }

        /// <summary>
        /// Whether the call failed (shown in the error colour).
        /// </summary>
        public bool IsError { get; init; }
    }
}
