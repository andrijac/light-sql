namespace LightSQL.Services.Llm
{
    /// <summary>
    /// The author of a chat message in an <see cref="LlmRequest"/>.
    /// Values start at 1 so a default (unset) value is never a valid choice.
    /// </summary>
    public enum LlmRole
    {
        /// <summary>
        /// The end user.
        /// </summary>
        User = 1,

        /// <summary>
        /// The model's reply (text and/or tool calls).
        /// </summary>
        Assistant = 2,

        /// <summary>
        /// A tool result returned to the model.
        /// </summary>
        Tool = 3
    }
}
