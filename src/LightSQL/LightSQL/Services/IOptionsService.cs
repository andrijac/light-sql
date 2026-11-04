using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// Loads and persists the user's <see cref="AppOptions"/>.
    /// </summary>
    public interface IOptionsService
    {
        /// <summary>
        /// The current options, loaded from disk on first access.
        /// </summary>
        AppOptions Current { get; }

        /// <summary>
        /// Reloads options from disk, returning defaults if no file exists.
        /// </summary>
        /// <returns>The loaded options.</returns>
        AppOptions Load();

        /// <summary>
        /// Persists the given options to disk and makes them current.
        /// </summary>
        /// <param name="options">The options to save.</param>
        void Save(AppOptions options);
    }
}
