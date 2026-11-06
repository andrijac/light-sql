using CommunityToolkit.Mvvm.ComponentModel;
using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// Backs the Options dialog, editing a working copy of <see cref="AppOptions"/>
    /// and persisting it on save. Choosing a theme previews it immediately; closing
    /// the dialog without saving restores the previous theme.
    /// </summary>
    public partial class OptionsViewModel : ObservableObject
    {
        private readonly IOptionsService optionsService;

        private readonly IThemeService themeService;

        /// <summary>
        /// The theme that was active when the dialog opened (restored on cancel).
        /// </summary>
        private readonly AppTheme originalTheme;

        [ObservableProperty]
        private bool warnOnUpdateDeleteWithoutWhere;

        [ObservableProperty]
        private bool showLineNumbers;

        [ObservableProperty]
        private double editorFontSize;

        [ObservableProperty]
        private AppTheme theme;

        /// <summary>
        /// Creates the view model, seeded from the current options.
        /// </summary>
        /// <param name="optionsService">Service used to load and persist options.</param>
        /// <param name="themeService">Applies (previews) the chosen theme.</param>
        public OptionsViewModel(IOptionsService optionsService, IThemeService themeService)
        {
            this.optionsService = optionsService;
            this.themeService = themeService;

            AppOptions options = optionsService.Current;

            warnOnUpdateDeleteWithoutWhere = options.WarnOnUpdateDeleteWithoutWhere;
            showLineNumbers = options.ShowLineNumbers;
            editorFontSize = options.EditorFontSize;
            theme = options.Theme;
            originalTheme = themeService.SelectedTheme;
        }

        /// <summary>
        /// The themes offered in the dialog.
        /// </summary>
        public IReadOnlyList<ThemeOption> Themes
        {
            get
            {
                return ThemeOption.All;
            }
        }

        /// <summary>
        /// Whether the options were saved when the dialog closed.
        /// </summary>
        public bool Saved { get; private set; }

        /// <summary>
        /// Persists the edited options.
        /// </summary>
        public void Save()
        {
            AppOptions options = new()
            {
                WarnOnUpdateDeleteWithoutWhere = WarnOnUpdateDeleteWithoutWhere,
                ShowLineNumbers = ShowLineNumbers,
                EditorFontSize = EditorFontSize,
                Theme = Theme
            };

            optionsService.Save(options);
            Saved = true;
        }

        /// <summary>
        /// Restores the theme that was active when the dialog opened, unless the
        /// options were saved. Called when the dialog closes.
        /// </summary>
        public void RevertThemePreview()
        {
            if (Saved == true)
            {
                return;
            }

            if (themeService.SelectedTheme != originalTheme)
            {
                themeService.ApplyTheme(originalTheme);
            }
        }

        /// <summary>
        /// Previews a newly chosen theme on all open windows.
        /// </summary>
        /// <param name="value">The chosen theme.</param>
        partial void OnThemeChanged(AppTheme value)
        {
            themeService.ApplyTheme(value);
        }
    }
}