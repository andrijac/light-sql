using System.Runtime.InteropServices;
using System.Windows;

namespace LightSQL.Services
{
    /// <summary>
    /// Default <see cref="IClipboardService"/> using the WPF clipboard.
    /// </summary>
    public sealed class ClipboardService : IClipboardService
    {
        /// <inheritdoc />
        public void SetText(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (COMException)
            {
                // Another program holds the clipboard open; copying is not critical,
                // so a failed copy is ignored rather than crashing the app.
            }
        }
    }
}
