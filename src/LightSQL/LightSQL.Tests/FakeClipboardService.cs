using LightSQL.Services;

namespace LightSQL.Tests
{
    /// <summary>
    /// In-memory <see cref="IClipboardService"/> that remembers the copied text.
    /// </summary>
    public sealed class FakeClipboardService : IClipboardService
    {
        /// <summary>
        /// The last text copied, or null when nothing was copied.
        /// </summary>
        public string? Text { get; private set; }

        /// <inheritdoc />
        public void SetText(string text)
        {
            Text = text;
        }
    }
}
