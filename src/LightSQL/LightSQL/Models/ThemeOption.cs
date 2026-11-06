namespace LightSQL.Models
{
    /// <summary>
    /// A theme as offered in the Options dialog: the value and its display name.
    /// </summary>
    public sealed class ThemeOption
    {
        /// <summary>
        /// Creates an option.
        /// </summary>
        /// <param name="theme">The theme value.</param>
        /// <param name="displayName">The name shown to the user.</param>
        public ThemeOption(AppTheme theme, string displayName)
        {
            Theme = theme;
            DisplayName = displayName;
        }

        /// <summary>
        /// Every selectable theme, in the order shown in the Options dialog.
        /// </summary>
        public static IReadOnlyList<ThemeOption> All { get; } = new List<ThemeOption>
        {
            new(AppTheme.System, "Auto (follow Windows)"),
            new(AppTheme.Light, "Light"),
            new(AppTheme.Dark, "Dark"),
            new(AppTheme.Classic, "Classic (Windows Forms gray)"),
            new(AppTheme.Gradient, "Gradient")
        };

        /// <summary>
        /// The theme value.
        /// </summary>
        public AppTheme Theme { get; }

        /// <summary>
        /// The name shown to the user.
        /// </summary>
        public string DisplayName { get; }
    }
}
