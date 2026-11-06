using Microsoft.Win32;

namespace LightSQL.Services
{
    /// <summary>
    /// Reads the Windows "Choose your app mode" setting from the registry and
    /// watches for changes through <see cref="SystemEvents.UserPreferenceChanged"/>.
    /// </summary>
    public sealed class WindowsSystemThemeProvider : ISystemThemeProvider, IDisposable
    {
        private bool usesLightTheme;

        private bool isDisposed;

        /// <summary>
        /// Registry key that holds the personalisation settings.
        /// </summary>
        private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        /// <summary>
        /// Registry value: 1 = apps use the light theme, 0 = dark.
        /// </summary>
        private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

        /// <summary>
        /// Reads the current setting and starts watching for changes.
        /// </summary>
        public WindowsSystemThemeProvider()
        {
            usesLightTheme = ReadUsesLightTheme();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        /// <inheritdoc />
        public event EventHandler? SystemThemeChanged;

        /// <inheritdoc />
        public bool UsesLightTheme
        {
            get
            {
                return usesLightTheme;
            }
        }

        /// <summary>
        /// Stops watching for changes (SystemEvents holds a static reference).
        /// </summary>
        public void Dispose()
        {
            if (isDisposed == true)
            {
                return;
            }

            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            isDisposed = true;
        }

        /// <summary>
        /// Reads the app-mode setting. A missing key or value (older Windows) means
        /// light, which is the Windows default.
        /// </summary>
        /// <returns>True when apps should use the light theme.</returns>
        private static bool ReadUsesLightTheme()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
                object? value = key?.GetValue(AppsUseLightThemeValueName);

                if (value is int number)
                {
                    return number != 0;
                }
            }
            catch (Exception)
            {
                // Unreadable registry: fall back to the Windows default below.
            }

            return true;
        }

        /// <summary>
        /// Re-reads the setting when Windows reports a preference change and raises
        /// <see cref="SystemThemeChanged"/> only if light/dark actually changed.
        /// </summary>
        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            bool current = ReadUsesLightTheme();

            if (current == usesLightTheme)
            {
                return;
            }

            usesLightTheme = current;
            SystemThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
