using LightSQL.Models;
using Microsoft.Data.SqlClient;

namespace LightSQL.Services
{
    /// <summary>
    /// Builds a .NET SQL Server connection string from a <see cref="ConnectionProfile"/>,
    /// supporting both form-based and raw connection-string profiles.
    /// </summary>
    public static class ConnectionStringFactory
    {
        private const string ApplicationName = "LightSQL";
        private const int DefaultConnectTimeoutSeconds = 15;

        /// <summary>
        /// Builds a connection string for the profile, optionally overriding the
        /// initial catalog (database).
        /// </summary>
        /// <param name="profile">The connection definition.</param>
        /// <param name="overrideDatabase">
        /// When non-empty, used as the initial catalog instead of the profile's own
        /// database. Used to target a specific database for metadata lookups.
        /// </param>
        /// <returns>The resulting connection string.</returns>
        public static string Build(ConnectionProfile profile, string? overrideDatabase = null)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            SqlConnectionStringBuilder builder;

            if (profile.UseRawConnectionString == true)
            {
                builder = new SqlConnectionStringBuilder(profile.RawConnectionString);

                // Re-inject a decrypted/typed password when the raw string has none.
                if (string.IsNullOrEmpty(profile.Password) == false && string.IsNullOrEmpty(builder.Password) == true)
                {
                    builder.Password = profile.Password;
                }
            }
            else
            {
                builder = new SqlConnectionStringBuilder
                {
                    DataSource = profile.Server,
                    Encrypt = profile.Encrypt,
                    TrustServerCertificate = profile.TrustServerCertificate,
                    ApplicationName = ApplicationName,
                    ConnectTimeout = DefaultConnectTimeoutSeconds
                };

                if (string.IsNullOrWhiteSpace(profile.Database) == false)
                {
                    builder.InitialCatalog = profile.Database;
                }

                if (profile.AuthenticationType == AuthenticationType.Windows)
                {
                    builder.IntegratedSecurity = true;
                }
                else
                {
                    builder.IntegratedSecurity = false;
                    builder.UserID = profile.UserId;
                    builder.Password = profile.Password ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(overrideDatabase) == false)
            {
                builder.InitialCatalog = overrideDatabase;
            }

            return builder.ConnectionString;
        }
    }
}
