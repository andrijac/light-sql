using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// Switches the application's colour theme at runtime. Every control is styled
    /// with dynamic resources (see Themes/Controls.xaml), so changing the theme
    /// recolours all open windows immediately.
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// The theme the user chose (may be <see cref="AppTheme.System"/>).
        /// </summary>
        AppTheme SelectedTheme { get; }

        /// <summary>
        /// The theme actually shown: <see cref="AppTheme.System"/> resolved to
        /// <see cref="AppTheme.Light"/> or <see cref="AppTheme.Dark"/>.
        /// </summary>
        AppTheme EffectiveTheme { get; }

        /// <summary>
        /// Whether the effective theme has dark backgrounds (used for the window
        /// title bars).
        /// </summary>
        bool IsDark { get; }

        /// <summary>
        /// Applies a theme to the whole application.
        /// </summary>
        /// <param name="theme">The theme to apply.</param>
        void ApplyTheme(AppTheme theme);
    }
}
