using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.Tests
{
    /// <summary>
    /// <see cref="IThemeService"/> that only records the applied themes (no WPF).
    /// </summary>
    public sealed class FakeThemeService : IThemeService
    {
        /// <summary>
        /// Every theme passed to <see cref="ApplyTheme"/>, in order.
        /// </summary>
        public List<AppTheme> AppliedThemes { get; } = new();

        /// <inheritdoc />
        public AppTheme SelectedTheme { get; set; } = AppTheme.System;

        /// <inheritdoc />
        public AppTheme EffectiveTheme
        {
            get
            {
                return ThemeService.ResolveEffectiveTheme(SelectedTheme, true);
            }
        }

        /// <inheritdoc />
        public bool IsDark
        {
            get
            {
                return ThemeService.IsDarkTheme(EffectiveTheme);
            }
        }

        /// <inheritdoc />
        public void ApplyTheme(AppTheme theme)
        {
            SelectedTheme = theme;
            AppliedThemes.Add(theme);
        }
    }
}
