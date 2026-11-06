using LightSQL.Services;

namespace LightSQL.Tests
{
    /// <summary>
    /// <see cref="ISystemThemeProvider"/> whose Windows light/dark mode is set by the
    /// test.
    /// </summary>
    public sealed class FakeSystemThemeProvider : ISystemThemeProvider
    {
        /// <inheritdoc />
        public event EventHandler? SystemThemeChanged;

        /// <inheritdoc />
        public bool UsesLightTheme { get; private set; } = true;

        /// <summary>
        /// Simulates the user switching the Windows app mode.
        /// </summary>
        /// <param name="usesLightTheme">True for light, false for dark.</param>
        public void SwitchTo(bool usesLightTheme)
        {
            UsesLightTheme = usesLightTheme;
            SystemThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
