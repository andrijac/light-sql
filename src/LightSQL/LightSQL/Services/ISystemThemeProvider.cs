namespace LightSQL.Services
{
    /// <summary>
    /// Reports whether Windows is set to light or dark app mode, and when that
    /// changes. Used by the "Auto" theme.
    /// </summary>
    public interface ISystemThemeProvider
    {
        /// <summary>
        /// Raised when the Windows app mode changes (light/dark). May be raised on a
        /// background thread.
        /// </summary>
        event EventHandler? SystemThemeChanged;

        /// <summary>
        /// Whether Windows apps should use the light theme (true) or the dark one.
        /// </summary>
        bool UsesLightTheme { get; }
    }
}
