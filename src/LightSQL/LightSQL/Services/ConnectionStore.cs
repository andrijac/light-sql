using System.IO;
using System.Text.Json;
using LightSQL.Models;
using Microsoft.Data.SqlClient;

namespace LightSQL.Services
{
    /// <summary>
    /// JSON-backed store of <see cref="ConnectionProfile"/> values, saved in
    /// "%APPDATA%\LightSQL\connections.json".
    ///
    /// Security: a plaintext password is never written. When a profile opts in to
    /// saving its password, the password is encrypted via <see cref="ICredentialProtector"/>
    /// into <see cref="ConnectionProfile.ProtectedPassword"/>, and any password
    /// embedded in a raw connection string is stripped out before serialization.
    /// </summary>
    public sealed class ConnectionStore : IConnectionStore
    {
        private static readonly JsonSerializerOptions serializerOptions = new()
        {
            WriteIndented = true
        };

        private readonly string filePath;
        private readonly ICredentialProtector credentialProtector;
        private readonly List<ConnectionProfile> profiles;

        /// <summary>
        /// Creates the store using the default connections file location.
        /// </summary>
        /// <param name="credentialProtector">Protector used to encrypt/decrypt passwords.</param>
        public ConnectionStore(ICredentialProtector credentialProtector)
            : this(credentialProtector, AppPaths.ConnectionsFilePath)
        {
        }

        /// <summary>
        /// Creates the store using an explicit file path (used by tests).
        /// </summary>
        /// <param name="credentialProtector">Protector used to encrypt/decrypt passwords.</param>
        /// <param name="filePath">Full path of the connections JSON file.</param>
        public ConnectionStore(ICredentialProtector credentialProtector, string filePath)
        {
            this.credentialProtector = credentialProtector ?? throw new ArgumentNullException(nameof(credentialProtector));
            this.filePath = filePath;
            profiles = Load();
        }

        /// <inheritdoc />
        public IReadOnlyList<ConnectionProfile> GetAll()
        {
            return profiles.AsReadOnly();
        }

        /// <inheritdoc />
        public void Save(ConnectionProfile profile)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            int existingIndex = profiles.FindIndex(p => string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                profiles[existingIndex] = profile;
            }
            else
            {
                profiles.Add(profile);
            }

            Persist();
        }

        /// <inheritdoc />
        public void Delete(string name)
        {
            int removed = profiles.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                Persist();
            }
        }

        /// <summary>
        /// Reads and decrypts profiles from disk, returning an empty list when no
        /// file exists or the file is unreadable.
        /// </summary>
        /// <returns>The loaded profiles.</returns>
        private List<ConnectionProfile> Load()
        {
            if (File.Exists(filePath) == false)
            {
                return new List<ConnectionProfile>();
            }

            try
            {
                string json = File.ReadAllText(filePath);
                List<ConnectionProfile>? loaded = JsonSerializer.Deserialize<List<ConnectionProfile>>(json, serializerOptions);

                if (loaded is null)
                {
                    return new List<ConnectionProfile>();
                }

                foreach (ConnectionProfile profile in loaded)
                {
                    // Decrypt any saved password into memory for this session only.
                    if (profile.SavePassword == true && string.IsNullOrEmpty(profile.ProtectedPassword) == false)
                    {
                        profile.Password = credentialProtector.Unprotect(profile.ProtectedPassword);
                    }
                }

                return loaded;
            }
            catch (Exception)
            {
                // A corrupt connections file should not stop the app from starting.
                return new List<ConnectionProfile>();
            }
        }

        /// <summary>
        /// Encrypts passwords, strips plaintext secrets, and writes all profiles to disk.
        /// </summary>
        private void Persist()
        {
            AppPaths.EnsureDirectory(AppPaths.ConfigDirectory);

            // Build sanitized copies so the in-memory profiles keep their plaintext
            // password for the session while the serialized form never contains one.
            List<ConnectionProfile> sanitized = new(profiles.Count);

            foreach (ConnectionProfile profile in profiles)
            {
                ConnectionProfile copy = profile.Clone();

                if (copy.SavePassword == true && string.IsNullOrEmpty(copy.Password) == false)
                {
                    copy.ProtectedPassword = credentialProtector.Protect(copy.Password);
                }
                else
                {
                    copy.ProtectedPassword = null;
                }

                // Never serialize a password embedded in a raw connection string.
                if (copy.UseRawConnectionString == true)
                {
                    copy.RawConnectionString = StripPassword(copy.RawConnectionString);
                }

                // The plaintext password is [JsonIgnore], but clear it defensively.
                copy.Password = null;

                sanitized.Add(copy);
            }

            string json = JsonSerializer.Serialize(sanitized, serializerOptions);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Removes any password from a raw connection string.
        /// </summary>
        /// <param name="connectionString">The raw connection string.</param>
        /// <returns>The connection string without a password.</returns>
        private static string StripPassword(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString) == true)
            {
                return connectionString;
            }

            try
            {
                SqlConnectionStringBuilder builder = new(connectionString);
                builder.Remove("Password");
                builder.Remove("PWD");

                return builder.ConnectionString;
            }
            catch (Exception)
            {
                // If the string cannot be parsed it cannot be used to connect either;
                // return empty rather than risk leaking an embedded password.
                return string.Empty;
            }
        }
    }
}
