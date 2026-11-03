namespace LightSQL.ViewModels
{
    /// <summary>
    /// Actions the Object Explorer can ask the main window to perform, such as
    /// setting the editor text or switching the active database. Implemented by
    /// <see cref="MainViewModel"/> and set on the explorer after construction to
    /// avoid a circular dependency.
    /// </summary>
    public interface IMainActions
    {
        /// <summary>
        /// Replaces the editor content with the given SQL.
        /// </summary>
        /// <param name="sql">The SQL to place in the editor.</param>
        void SetEditorText(string sql);

        /// <summary>
        /// Makes the given database the active one for execution and the toolbar.
        /// </summary>
        /// <param name="database">The database to make active.</param>
        void SetActiveDatabase(string database);

        /// <summary>
        /// Executes the current editor content.
        /// </summary>
        /// <returns>A task that completes when execution finishes.</returns>
        Task ExecuteAsync();

        /// <summary>
        /// Reloads the toolbar's database list (for example after creating one).
        /// </summary>
        /// <returns>A task that completes when the list is refreshed.</returns>
        Task RefreshDatabaseListAsync();
    }
}
