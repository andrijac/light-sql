using System.IO;
using System.Text.Json;
using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// JSON-backed implementation of <see cref="IOptionsService"/>. Options are
    /// stored in "%APPDATA%\LightSQL\options.json".
    /// </summary>
    public sealed class OptionsService : IOptionsService
    {
        private static readonly JsonSerializerOptions serializerOptions = new()
        {
            WriteIndented = true
        };

        private readonly string filePath;

        private AppOptions current;

        /// <summary>
        /// Creates the service using the default options file location.
        /// </summary>
        public OptionsService()
            : this(AppPaths.OptionsFilePath)
        {
        }

        /// <summary>
        /// Creates the service using an explicit file path (used by tests).
        /// </summary>
        /// <param name="filePath">Full path of the options JSON file.</param>
        public OptionsService(string filePath)
        {
            this.filePath = filePath;
            current = Load();
        }

        /// <inheritdoc />
        public AppOptions Current
        {
            get
            {
                return current;
            }
        }

        /// <inheritdoc />
        public AppOptions Load()
        {
            if (File.Exists(filePath) == false)
            {
                current = new AppOptions();

                return current;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                AppOptions? loaded = JsonSerializer.Deserialize<AppOptions>(json, serializerOptions);

                current = loaded ?? new AppOptions();
            }
            catch (Exception)
            {
                // A corrupt options file should not stop the app from starting.
                current = new AppOptions();
            }

            return current;
        }

        /// <inheritdoc />
        public void Save(AppOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            string? directory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directory) == false)
            {
                AppPaths.EnsureDirectory(directory);
            }

            string json = JsonSerializer.Serialize(options, serializerOptions);
            File.WriteAllText(filePath, json);

            current = options;
        }
    }
}
