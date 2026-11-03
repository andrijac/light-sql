namespace LightSQL.Models
{
    /// <summary>
    /// A query saved in the user's personal queries folder.
    /// </summary>
    public sealed class SavedQuery
    {
        /// <summary>
        /// The query name (the file name without its ".sql" extension).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Full path of the backing ".sql" file.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
    }
}
