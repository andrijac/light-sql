namespace LightSQL.Models
{
    /// <summary>
    /// One line shown in the chat transcript: a user message, an assistant reply, or an error.
    /// Tool calls are not messages — they go in a <see cref="ChatToolGroup"/>.
    /// </summary>
    public sealed class ChatMessage : ChatTranscriptItem
    {
        /// <summary>
        /// Who produced the line.
        /// </summary>
        public required ChatAuthor Author { get; init; }

        /// <summary>
        /// The text to show.
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// Whether the line is an error (shown in the error colour).
        /// </summary>
        public bool IsError { get; init; }
    }
}
