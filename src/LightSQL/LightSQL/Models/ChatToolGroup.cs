using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LightSQL.Models
{
    /// <summary>
    /// The collapsible panel of tool calls the assistant made in one turn. It is collapsed by
    /// default and its header shows the current (most recent) tool, so the user sees activity at
    /// a glance; expanding it reveals every call. Tool calls are excluded when the conversation
    /// is copied.
    /// </summary>
    public sealed partial class ChatToolGroup : ChatTranscriptItem
    {
        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private bool isExpanded;

        /// <summary>
        /// The tool calls in this turn, oldest first.
        /// </summary>
        public ObservableCollection<ChatToolCall> Calls { get; } = new();

        /// <summary>
        /// Adds a tool call and makes it the collapsed header (the "current" tool).
        /// </summary>
        /// <param name="call">The tool call to add.</param>
        public void Add(ChatToolCall call)
        {
            Calls.Add(call);
            Title = call.ToolName;
        }

        /// <summary>
        /// Shows or hides the tool calls.
        /// </summary>
        [RelayCommand]
        private void Toggle()
        {
            IsExpanded = IsExpanded == false;
        }
    }
}
