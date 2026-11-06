namespace LightSQL.Models
{
    /// <summary>
    /// The colour themes the application can use. Values start at 1 so an unset
    /// (default) value is never mistaken for a valid theme.
    /// </summary>
    public enum AppTheme
    {
        /// <summary>
        /// A modern light theme.
        /// </summary>
        Light = 1,

        /// <summary>
        /// A dark theme (dark backgrounds, light text).
        /// </summary>
        Dark = 2,

        /// <summary>
        /// The classic Windows Forms look: grey control surfaces, white inputs.
        /// </summary>
        Classic = 3,

        /// <summary>
        /// Follows the Windows "app mode" setting: Light or Dark.
        /// </summary>
        System = 4,

        /// <summary>
        /// A dark, colourful theme with a gradient window background.
        /// </summary>
        Gradient = 5
    }
}
