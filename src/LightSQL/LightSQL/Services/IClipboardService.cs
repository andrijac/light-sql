namespace LightSQL.Services
{
    /// <summary>
    /// Puts text on the Windows clipboard (behind an interface so view models stay
    /// testable).
    /// </summary>
    public interface IClipboardService
    {
        /// <summary>
        /// Replaces the clipboard content with the given text.
        /// </summary>
        /// <param name="text">The text to copy.</param>
        void SetText(string text);
    }
}
