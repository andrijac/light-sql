using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.Tests
{
    /// <summary>
    /// In-memory <see cref="IQueryStore"/> for view-model tests.
    /// </summary>
    public sealed class FakeQueryStore : IQueryStore
    {
        /// <summary>
        /// Saved queries by name (case-insensitive, like file names on Windows).
        /// </summary>
        public Dictionary<string, string> Queries { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public string Folder
        {
            get
            {
                return "memory";
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<SavedQuery> GetAll()
        {
            return Queries.Keys
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => new SavedQuery { Name = name, FilePath = name + ".sql" })
                .ToList();
        }

        /// <inheritdoc />
        public SavedQuery Save(string name, string content)
        {
            Queries[name] = content;

            return new SavedQuery { Name = name, FilePath = name + ".sql" };
        }

        /// <inheritdoc />
        public string ReadContent(string name)
        {
            return Queries[name];
        }

        /// <inheritdoc />
        public void Delete(string name)
        {
            Queries.Remove(name);
        }

        /// <inheritdoc />
        public bool IsValidName(string name)
        {
            return string.IsNullOrWhiteSpace(name) == false;
        }
    }
}
