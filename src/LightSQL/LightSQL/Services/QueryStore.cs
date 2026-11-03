using System.IO;
using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// File-system implementation of <see cref="IQueryStore"/>. Queries are stored
    /// as ".sql" files in the folder resolved by <see cref="IOptionsService"/>.
    /// </summary>
    public sealed class QueryStore : IQueryStore
    {
        private const string FileExtension = ".sql";

        private readonly IOptionsService optionsService;

        /// <summary>
        /// Creates the store.
        /// </summary>
        /// <param name="optionsService">Supplies the configured queries folder.</param>
        public QueryStore(IOptionsService optionsService)
        {
            this.optionsService = optionsService ?? throw new ArgumentNullException(nameof(optionsService));
        }

        /// <inheritdoc />
        public string Folder
        {
            get
            {
                return optionsService.ResolveQueriesFolder();
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<SavedQuery> GetAll()
        {
            string folder = Folder;

            if (Directory.Exists(folder) == false)
            {
                return Array.Empty<SavedQuery>();
            }

            List<SavedQuery> queries = new();

            foreach (string file in Directory.EnumerateFiles(folder, "*" + FileExtension))
            {
                queries.Add(new SavedQuery
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file
                });
            }

            queries.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

            return queries;
        }

        /// <inheritdoc />
        public SavedQuery Save(string name, string content)
        {
            if (IsValidName(name) == false)
            {
                throw new ArgumentException("The query name is empty or contains invalid characters.", nameof(name));
            }

            string folder = Folder;
            AppPaths.EnsureDirectory(folder);

            string filePath = Path.Combine(folder, name.Trim() + FileExtension);
            File.WriteAllText(filePath, content ?? string.Empty);

            return new SavedQuery
            {
                Name = name.Trim(),
                FilePath = filePath
            };
        }

        /// <inheritdoc />
        public string ReadContent(string name)
        {
            if (IsValidName(name) == false)
            {
                throw new ArgumentException("The query name is empty or contains invalid characters.", nameof(name));
            }

            string filePath = Path.Combine(Folder, name.Trim() + FileExtension);

            if (File.Exists(filePath) == false)
            {
                throw new FileNotFoundException("The saved query was not found.", filePath);
            }

            return File.ReadAllText(filePath);
        }

        /// <inheritdoc />
        public void Delete(string name)
        {
            if (IsValidName(name) == false)
            {
                return;
            }

            string filePath = Path.Combine(Folder, name.Trim() + FileExtension);

            if (File.Exists(filePath) == true)
            {
                File.Delete(filePath);
            }
        }

        /// <inheritdoc />
        public bool IsValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) == true)
            {
                return false;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();

            foreach (char c in name)
            {
                if (Array.IndexOf(invalidChars, c) >= 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
