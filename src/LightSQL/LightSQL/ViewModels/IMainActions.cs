namespace LightSQL.ViewModels
{
    /// <summary>
    /// Actions the Object Explorer can ask the main window to perform, such as
    /// opening a query tab or executing the current query. Implemented by
    /// <see cref="MainViewModel"/> and set on the explorer after construction to
    /// avoid a circular dependency.
    /// </summary>
    public interface IMainActions
    {
        /// <summary>
        /// Opens a new query tab with the given SQL and database and selects it.
        /// </summary>
        /// <param name="database">The tab's database, or null for the default.</param>
        /// <param name="sql">The SQL to place in the new tab's editor.</param>
        void OpenQueryTab(string? database, string sql);

        /// <summary>
        /// Executes the query in the selected tab.
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
