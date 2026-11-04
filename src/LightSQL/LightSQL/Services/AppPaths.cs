using System.IO;

namespace LightSQL.Services
{
    /// <summary>
    /// Central source of the file-system locations the application reads and writes,
    /// all rooted in the current user's profile.
    /// </summary>
    public static class AppPaths
    {
        /// <summary>
        /// The application's configuration directory: "%APPDATA%\LightSQL".
        /// </summary>
        public static string ConfigDirectory
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                return Path.Combine(appData, "LightSQL");
            }
        }

        /// <summary>
        /// Full path of the options JSON file.
        /// </summary>
        public static string OptionsFilePath
        {
            get
            {
                return Path.Combine(ConfigDirectory, "options.json");
            }
        }

        /// <summary>
        /// Full path of the saved-connections JSON file.
        /// </summary>
        public static string ConnectionsFilePath
        {
            get
            {
                return Path.Combine(ConfigDirectory, "connections.json");
            }
        }

        /// <summary>
        /// Ensures a directory exists, creating it (and any parents) if needed.
        /// </summary>
        /// <param name="path">The directory path to ensure.</param>
        public static void EnsureDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) == true)
            {
                return;
            }

            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
