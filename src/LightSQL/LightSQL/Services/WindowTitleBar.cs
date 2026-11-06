using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LightSQL.Services
{
    /// <summary>
    /// Switches a window's title bar (drawn by Windows, not WPF) between light and
    /// dark, so it matches the application theme. Has no effect on Windows versions
    /// that do not support dark title bars.
    /// </summary>
    public static class WindowTitleBar
    {
        /// <summary>
        /// DWM attribute for dark title bars on Windows 10 20H1+ and Windows 11.
        /// </summary>
        private const int DwmUseImmersiveDarkMode = 20;

        /// <summary>
        /// The same attribute's id on Windows 10 1809-1909.
        /// </summary>
        private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

        /// <summary>
        /// Makes the window's title bar dark or light.
        /// </summary>
        /// <param name="window">The window (must have a handle, i.e. be shown).</param>
        /// <param name="isDark">True for a dark title bar.</param>
        public static void Apply(Window window, bool isDark)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;

            if (handle == IntPtr.Zero)
            {
                return;
            }

            int value = isDark == true ? 1 : 0;
            int result = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref value, sizeof(int));

            if (result != 0)
            {
                DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeBefore20H1, ref value, sizeof(int));
            }
        }

        /// <summary>
        /// Sets a Desktop Window Manager attribute of a window.
        /// </summary>
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
