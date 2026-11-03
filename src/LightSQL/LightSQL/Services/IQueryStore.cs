using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// Saves and loads named queries as ".sql" files in the user's personal
    /// queries folder.
    /// </summary>
    public interface IQueryStore
    {
        /// <summary>
        /// The absolute path of the folder where queries are stored.
        /// </summary>
        string Folder { get; }

        /// <summary>
        /// Lists all saved queries, ordered by name.
        /// </summary>
        /// <returns>The saved queries.</returns>
        IReadOnlyList<SavedQuery> GetAll();

        /// <summary>
        /// Saves (creating or overwriting) a named query.
        /// </summary>
        /// <param name="name">The query name. Must be a valid, non-empty file name.</param>
        /// <param name="content">The SQL text to save.</param>
        /// <returns>The saved query descriptor.</returns>
        SavedQuery Save(string name, string content);

        /// <summary>
        /// Reads the SQL text of a saved query by name.
        /// </summary>
        /// <param name="name">The query name.</param>
        /// <returns>The saved SQL text.</returns>
        string ReadContent(string name);

        /// <summary>
        /// Deletes a saved query by name, if present.
        /// </summary>
        /// <param name="name">The query name.</param>
        void Delete(string name);

        /// <summary>
        /// Determines whether a name is a valid query name (non-empty and free of
        /// invalid file-name characters).
        /// </summary>
        /// <param name="name">The candidate name.</param>
        /// <returns>True if the name is valid.</returns>
        bool IsValidName(string name);
    }
}
