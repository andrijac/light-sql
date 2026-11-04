namespace LightSQL.Models
{
    /// <summary>
    /// User-configurable application options, persisted as JSON in the user profile.
    /// </summary>
    public sealed class AppOptions
    {
        /// <summary>
        /// When true, executing an UPDATE or DELETE statement that has no WHERE
        /// clause prompts for confirmation first. Defaults to true for safety.
        /// </summary>
        public bool WarnOnUpdateDeleteWithoutWhere { get; set; } = true;

        /// <summary>
        /// Whether the editor shows line numbers. Defaults to true.
        /// </summary>
        public bool ShowLineNumbers { get; set; } = true;

        /// <summary>
        /// Editor font size in points. Defaults to 13.
        /// </summary>
        public double EditorFontSize { get; set; } = 13d;
    }
}
