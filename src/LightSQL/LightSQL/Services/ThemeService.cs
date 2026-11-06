using System.Collections.ObjectModel;
using System.Windows;
using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// Applies a colour theme by swapping the palette resource dictionary
    /// (Themes/Palettes/*.xaml) in the application resources. The control styles in
    /// Themes/Controls.xaml reference palette brushes with DynamicResource, so all
    /// open windows update immediately. The "Auto" theme follows the Windows app
    /// mode and re-applies itself when that changes.
    /// </summary>
    public sealed class ThemeService : IThemeService, IDisposable
    {
        private readonly ISystemThemeProvider systemThemeProvider;

        private readonly ResourceDictionary resources;

        private AppTheme selectedTheme = AppTheme.System;

        private AppTheme effectiveTheme = AppTheme.Light;

        /// <summary>
        /// Resource key every palette defines (a string with the palette's name). It
        /// identifies the palette dictionary among the merged dictionaries.
        /// </summary>
        public const string ThemeNameKey = "Theme.Name";

        /// <summary>
        /// Folder (as a component URI) that holds one palette file per theme.
        /// </summary>
        private const string PaletteFolderUri = "/LightSQL;component/Themes/Palettes/";

        /// <summary>
        /// Creates the service for the running application's resources.
        /// </summary>
        /// <param name="systemThemeProvider">Reports the Windows light/dark mode.</param>
        public ThemeService(ISystemThemeProvider systemThemeProvider)
            : this(systemThemeProvider, Application.Current.Resources)
        {
        }

        /// <summary>
        /// Creates the service for an explicit resource dictionary (used by tests).
        /// </summary>
        /// <param name="systemThemeProvider">Reports the Windows light/dark mode.</param>
        /// <param name="resources">The dictionary whose palette is swapped.</param>
        public ThemeService(ISystemThemeProvider systemThemeProvider, ResourceDictionary resources)
        {
            this.systemThemeProvider = systemThemeProvider;
            this.resources = resources;

            systemThemeProvider.SystemThemeChanged += OnSystemThemeChanged;
        }

        /// <inheritdoc />
        public AppTheme SelectedTheme
        {
            get
            {
                return selectedTheme;
            }
        }

        /// <inheritdoc />
        public AppTheme EffectiveTheme
        {
            get
            {
                return effectiveTheme;
            }
        }

        /// <inheritdoc />
        public bool IsDark
        {
            get
            {
                return IsDarkTheme(effectiveTheme);
            }
        }

        /// <summary>
        /// Resolves the theme to show: <see cref="AppTheme.System"/> (and any unknown
        /// value) becomes Light or Dark depending on Windows; the others are kept.
        /// </summary>
        /// <param name="selected">The chosen theme.</param>
        /// <param name="systemUsesLightTheme">Whether Windows is in light app mode.</param>
        /// <returns>A concrete theme that has a palette.</returns>
        public static AppTheme ResolveEffectiveTheme(AppTheme selected, bool systemUsesLightTheme)
        {
            switch (selected)
            {
                case AppTheme.Light:
                case AppTheme.Dark:
                case AppTheme.Classic:
                case AppTheme.Gradient:
                    return selected;

                default:
                    return systemUsesLightTheme == true ? AppTheme.Light : AppTheme.Dark;
            }
        }

        /// <summary>
        /// Gets the component URI of a concrete theme's palette file.
        /// </summary>
        /// <param name="effectiveTheme">A concrete theme (not System).</param>
        /// <returns>The palette's relative component URI.</returns>
        public static Uri GetPaletteUri(AppTheme effectiveTheme)
        {
            switch (effectiveTheme)
            {
                case AppTheme.Light:
                case AppTheme.Dark:
                case AppTheme.Classic:
                case AppTheme.Gradient:
                    return new Uri($"{PaletteFolderUri}{effectiveTheme}.xaml", UriKind.Relative);

                default:
                    throw new ArgumentOutOfRangeException(nameof(effectiveTheme), effectiveTheme, "Only concrete themes have a palette.");
            }
        }

        /// <summary>
        /// Whether a concrete theme has dark backgrounds.
        /// </summary>
        /// <param name="effectiveTheme">A concrete theme.</param>
        /// <returns>True for Dark and Gradient.</returns>
        public static bool IsDarkTheme(AppTheme effectiveTheme)
        {
            return effectiveTheme == AppTheme.Dark || effectiveTheme == AppTheme.Gradient;
        }

        /// <inheritdoc />
        public void ApplyTheme(AppTheme theme)
        {
            selectedTheme = Enum.IsDefined(theme) == true ? theme : AppTheme.System;
            effectiveTheme = ResolveEffectiveTheme(selectedTheme, systemThemeProvider.UsesLightTheme);

            ResourceDictionary palette = (ResourceDictionary)Application.LoadComponent(GetPaletteUri(effectiveTheme));

            ReplacePalette(palette);
            UpdateOpenWindowTitleBars();
        }

        /// <summary>
        /// Stops listening for Windows theme changes.
        /// </summary>
        public void Dispose()
        {
            systemThemeProvider.SystemThemeChanged -= OnSystemThemeChanged;
        }

        /// <summary>
        /// Puts the new palette where the old one was (or first, if there was none).
        /// Replacing the dictionary makes WPF re-resolve every DynamicResource.
        /// </summary>
        /// <param name="palette">The new palette dictionary.</param>
        private void ReplacePalette(ResourceDictionary palette)
        {
            Collection<ResourceDictionary> merged = resources.MergedDictionaries;

            for (int i = 0; i < merged.Count; i++)
            {
                if (merged[i].Contains(ThemeNameKey) == true)
                {
                    merged[i] = palette;

                    return;
                }
            }

            merged.Insert(0, palette);
        }

        /// <summary>
        /// Updates the Windows-drawn title bars of the open windows. Windows opened
        /// later are handled by the class handler registered in App.
        /// </summary>
        private void UpdateOpenWindowTitleBars()
        {
            Application? application = Application.Current;

            if (application is null)
            {
                return;
            }

            foreach (Window window in application.Windows)
            {
                WindowTitleBar.Apply(window, IsDark);
            }
        }

        /// <summary>
        /// Re-applies the Auto theme when Windows switches between light and dark.
        /// The event can arrive on a background thread, so it is marshalled to the UI
        /// thread when there is an application.
        /// </summary>
        private void OnSystemThemeChanged(object? sender, EventArgs e)
        {
            if (selectedTheme != AppTheme.System)
            {
                return;
            }

            Application? application = Application.Current;

            if (application is null)
            {
                ApplyTheme(AppTheme.System);

                return;
            }

            application.Dispatcher.BeginInvoke(() => ApplyTheme(AppTheme.System));
        }
    }
}
