using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// Persists the user's saved connection profiles. Passwords are only stored when
    /// the profile opts in, and always in encrypted form.
    /// </summary>
    public interface IConnectionStore
    {
        /// <summary>
        /// Returns all saved connection profiles, with any saved passwords decrypted
        /// into memory for the current session.
        /// </summary>
        /// <returns>The saved profiles.</returns>
        IReadOnlyList<ConnectionProfile> GetAll();

        /// <summary>
        /// Adds a new profile or updates the existing one with the same name.
        /// </summary>
        /// <param name="profile">The profile to save.</param>
        void Save(ConnectionProfile profile);

        /// <summary>
        /// Deletes the saved profile with the given name, if present.
        /// </summary>
        /// <param name="name">The profile name to delete.</param>
        void Delete(string name);
    }
}
