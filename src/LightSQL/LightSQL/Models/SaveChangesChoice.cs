namespace LightSQL.Models
{
    /// <summary>
    /// The user's answer when closing a query tab that has unsaved changes.
    /// </summary>
    public enum SaveChangesChoice
    {
        /// <summary>
        /// Save the query, then close it.
        /// </summary>
        Save = 1,

        /// <summary>
        /// Close the query and discard the changes.
        /// </summary>
        Discard = 2,

        /// <summary>
        /// Keep the query open.
        /// </summary>
        Cancel = 3
    }
}
