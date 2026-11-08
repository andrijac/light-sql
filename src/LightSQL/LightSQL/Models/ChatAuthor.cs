namespace LightSQL.Models
{
    /// <summary>
    /// Who produced a <see cref="ChatMessage"/> line in the chat transcript. Values start at 1
    /// so a default (unset) value is never a valid choice. (Tool calls are not messages; they
    /// go in a <see cref="ChatToolGroup"/>.)
    /// </summary>
    public enum ChatAuthor
    {
        /// <summary>
        /// The user's message.
        /// </summary>
        User = 1,

        /// <summary>
        /// The assistant's reply.
        /// </summary>
        Assistant = 2,

        /// <summary>
        /// An error (e.g. the provider request failed).
        /// </summary>
        Error = 3
    }
}
