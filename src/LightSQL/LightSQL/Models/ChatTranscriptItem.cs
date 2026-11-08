using CommunityToolkit.Mvvm.ComponentModel;

namespace LightSQL.Models
{
    /// <summary>
    /// Base type for anything shown in the chat transcript, so the transcript can hold both
    /// kinds in one collection: a <see cref="ChatMessage"/> (a user, assistant or error line)
    /// and a <see cref="ChatToolGroup"/> (the collapsible panel of tool calls for one turn).
    /// Derives from <see cref="ObservableObject"/> so items whose state changes after they are
    /// shown (the tool group) can notify the UI.
    /// </summary>
    public abstract class ChatTranscriptItem : ObservableObject
    {
    }
}
