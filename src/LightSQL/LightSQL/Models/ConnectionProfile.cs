using System.Text.Json.Serialization;

namespace LightSQL.Models
{
    /// <summary>
    /// A saved (or in-progress) connection definition. Can represent either a
    /// form-based connection (server + auth) or a raw .NET connection string.
    ///
    /// Security: the plaintext <see cref="Password"/> is never serialized
    /// (<see cref="JsonIgnoreAttribute"/>). Only <see cref="ProtectedPassword"/>,
    /// a DPAPI-encrypted value, is written to disk and only when the user opts in.
    /// </summary>
    public sealed class ConnectionProfile
    {
        /// <summary>
        /// Friendly name used to identify the connection in the saved list.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The database engine this connection targets. Selects the provider used to
        /// connect, read metadata and build SQL. Defaults to
        /// <see cref="DatabaseEngine.SqlServer"/> so profiles saved before multi-engine
        /// support (which have no stored value) keep working.
        /// </summary>
        public DatabaseEngine Engine { get; set; } = DatabaseEngine.SqlServer;

        /// <summary>
        /// When true, the connection is defined by <see cref="RawConnectionString"/>
        /// rather than by the individual form fields.
        /// </summary>
        public bool UseRawConnectionString { get; set; }

        /// <summary>
        /// Server / host name (form mode), e.g. "localhost", ".\SQLEXPRESS" or an IP.
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// TCP port (form mode), or null to use the engine's default port. Mainly used
        /// by PostgreSQL and MySQL; SQL Server usually takes the port in the instance name.
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// Authentication method (form mode).
        /// </summary>
        public AuthenticationType AuthenticationType { get; set; } = AuthenticationType.Windows;

        /// <summary>
        /// Login name used for SQL Server authentication (form mode).
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Initial catalog / database to connect to. May be empty for the server default.
        /// </summary>
        public string Database { get; set; } = string.Empty;

        /// <summary>
        /// Whether the connection should request encryption.
        /// </summary>
        public bool Encrypt { get; set; } = true;

        /// <summary>
        /// Whether to trust the server certificate (useful for local/dev servers
        /// with self-signed certificates).
        /// </summary>
        public bool TrustServerCertificate { get; set; } = true;

        /// <summary>
        /// Raw .NET connection string (raw mode). When persisted, any embedded
        /// password is stripped out and moved into <see cref="ProtectedPassword"/>.
        /// </summary>
        public string RawConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Whether the user opted in to saving the password for this connection.
        /// </summary>
        public bool SavePassword { get; set; }

        /// <summary>
        /// DPAPI-encrypted password (Base64). This is the only password form that is
        /// ever written to disk, and only when <see cref="SavePassword"/> is true.
        /// </summary>
        public string? ProtectedPassword { get; set; }

        /// <summary>
        /// Plaintext password held only in memory for the current session.
        /// Never serialized.
        /// </summary>
        [JsonIgnore]
        public string? Password { get; set; }

        /// <summary>
        /// Creates a deep copy so dialogs can edit a connection without mutating
        /// the stored instance until the user confirms.
        /// </summary>
        /// <returns>A new <see cref="ConnectionProfile"/> with identical values.</returns>
        public ConnectionProfile Clone()
        {
            return new ConnectionProfile
            {
                Name = Name,
                Engine = Engine,
                UseRawConnectionString = UseRawConnectionString,
                Server = Server,
                Port = Port,
                AuthenticationType = AuthenticationType,
                UserId = UserId,
                Database = Database,
                Encrypt = Encrypt,
                TrustServerCertificate = TrustServerCertificate,
                RawConnectionString = RawConnectionString,
                SavePassword = SavePassword,
                ProtectedPassword = ProtectedPassword,
                Password = Password
            };
        }
    }
}
