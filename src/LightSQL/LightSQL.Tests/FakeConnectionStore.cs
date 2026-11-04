using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.Tests
{
    /// <summary>
    /// In-memory <see cref="IConnectionStore"/> for view-model tests.
    /// </summary>
    public sealed class FakeConnectionStore : IConnectionStore
    {
        /// <summary>
        /// Creates the store with optional initial profiles.
        /// </summary>
        /// <param name="profiles">Profiles the store starts with.</param>
        public FakeConnectionStore(params ConnectionProfile[] profiles)
        {
            Profiles.AddRange(profiles);
        }

        /// <summary>
        /// The stored profiles.
        /// </summary>
        public List<ConnectionProfile> Profiles { get; } = new();

        /// <inheritdoc />
        public IReadOnlyList<ConnectionProfile> GetAll()
        {
            return Profiles.AsReadOnly();
        }

        /// <inheritdoc />
        public void Save(ConnectionProfile profile)
        {
            Profiles.RemoveAll(p => string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
            Profiles.Add(profile);
        }

        /// <inheritdoc />
        public void Delete(string name)
        {
            Profiles.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}