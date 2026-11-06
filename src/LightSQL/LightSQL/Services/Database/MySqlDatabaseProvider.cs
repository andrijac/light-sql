using System.Data.Common;
using LightSQL.Models;
using MySqlConnector;

namespace LightSQL.Services.Database
{
    /// <summary>
    /// MySQL / MariaDB provider (MySqlConnector). Lists databases (schemas) and reads
    /// tables / views / columns from <c>information_schema</c>, and surfaces server messages
    /// via <see cref="MySqlConnection.InfoMessage"/>. MySQL has no schema layer, so a
    /// database is a schema and object references carry no schema part.
    /// </summary>
    public sealed class MySqlDatabaseProvider : DatabaseProviderBase
    {
        private const int DefaultConnectTimeoutSeconds = 15;

        private readonly MySqlDialect dialect = new();

        /// <inheritdoc />
        public override DatabaseEngine Engine
        {
            get
            {
                return DatabaseEngine.MySql;
            }
        }

        /// <inheritdoc />
        public override string DisplayName
        {
            get
            {
                return "MySQL";
            }
        }

        /// <inheritdoc />
        public override ISqlDialect Dialect
        {
            get
            {
                return dialect;
            }
        }

        /// <inheritdoc />
        public override bool SupportsWindowsAuthentication
        {
            get
            {
                return false;
            }
        }

        /// <inheritdoc />
        public override int DefaultPort
        {
            get
            {
                return 3306;
            }
        }

        /// <inheritdoc />
        public override string? AdministrativeDatabase
        {
            get
            {
                // Server-level work (create / list databases) runs from any connection.
                return null;
            }
        }

        /// <inheritdoc />
        protected override string CurrentDatabaseQuery
        {
            get
            {
                return "SELECT DATABASE()";
            }
        }

        /// <inheritdoc />
        public override DbConnection CreateConnection(string connectionString)
        {
            return new MySqlConnection(connectionString);
        }

        /// <inheritdoc />
        public override string BuildConnectionString(ConnectionProfile profile, string? overrideDatabase)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            MySqlConnectionStringBuilder builder;

            if (profile.UseRawConnectionString == true)
            {
                builder = new MySqlConnectionStringBuilder(profile.RawConnectionString);

                // Re-inject a decrypted/typed password when the raw string has none.
                if (string.IsNullOrEmpty(profile.Password) == false && string.IsNullOrEmpty(builder.Password) == true)
                {
                    builder.Password = profile.Password;
                }
            }
            else
            {
                builder = new MySqlConnectionStringBuilder
                {
                    Server = profile.Server,
                    Port = (uint)(profile.Port ?? DefaultPort),
                    UserID = profile.UserId,
                    Password = profile.Password ?? string.Empty,
                    SslMode = ResolveSslMode(profile),
                    ConnectionTimeout = DefaultConnectTimeoutSeconds
                };

                if (string.IsNullOrWhiteSpace(profile.Database) == false)
                {
                    builder.Database = profile.Database;
                }
            }

            if (string.IsNullOrWhiteSpace(overrideDatabase) == false)
            {
                builder.Database = overrideDatabase;
            }

            return builder.ConnectionString;
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<string>> GetDatabasesAsync(DbConnection connection, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT schema_name FROM information_schema.schemata ORDER BY schema_name";

            return await ReadNamesAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<SchemaObject>> GetTablesAsync(DbConnection connection, string database, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();

            // MySQL has no schema layer; select NULL so the object keeps an empty schema.
            command.CommandText = @"SELECT NULL AS table_schema, table_name
                FROM information_schema.tables
                WHERE table_schema = @db AND table_type = 'BASE TABLE'
                ORDER BY table_name";

            AddParameter(command, "@db", database);

            return await ReadSchemaObjectsAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<SchemaObject>> GetViewsAsync(DbConnection connection, string database, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = @"SELECT NULL AS table_schema, table_name
                FROM information_schema.views
                WHERE table_schema = @db
                ORDER BY table_name";

            AddParameter(command, "@db", database);

            return await ReadSchemaObjectsAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(DbConnection connection, string database, string schema, string objectName, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();

            // column_type already carries length / precision (e.g. "varchar(50)", "decimal(18,2)").
            command.CommandText = @"SELECT column_name, column_type, is_nullable
                FROM information_schema.columns
                WHERE table_schema = @db AND table_name = @object
                ORDER BY ordinal_position";

            AddParameter(command, "@db", database);
            AddParameter(command, "@object", objectName);

            List<ColumnInfo> columns = new();

            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken) == true)
            {
                bool isNullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase) == true;

                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = isNullable
                });
            }

            return columns;
        }

        /// <inheritdoc />
        public override void AttachMessageHandler(DbConnection connection, Action<string> onMessage)
        {
            if (connection is MySqlConnection mysqlConnection)
            {
                mysqlConnection.InfoMessage += (sender, args) =>
                {
                    foreach (MySqlError error in args.Errors)
                    {
                        onMessage(error.Message);
                    }
                };
            }
        }

        /// <inheritdoc />
        public override IEnumerable<string> GetErrorMessages(Exception exception)
        {
            if (exception is MySqlException mysqlException)
            {
                yield return mysqlException.Message;
            }
        }

        /// <inheritdoc />
        public override string? ExtractPassword(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString) == true)
            {
                return null;
            }

            try
            {
                MySqlConnectionStringBuilder builder = new(connectionString);

                return string.IsNullOrEmpty(builder.Password) == true ? null : builder.Password;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public override string StripPassword(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString) == true)
            {
                return connectionString;
            }

            try
            {
                MySqlConnectionStringBuilder builder = new(connectionString)
                {
                    Password = string.Empty
                };

                return builder.ConnectionString;
            }
            catch (Exception)
            {
                // If the string cannot be parsed it cannot be used to connect either;
                // return empty rather than risk leaking an embedded password.
                return string.Empty;
            }
        }

        /// <summary>
        /// Maps the profile's Encrypt / Trust-certificate choices onto a MySQL SSL mode:
        /// no encryption, encrypt-and-trust (Preferred), or encrypt-and-verify (VerifyFull).
        /// </summary>
        /// <param name="profile">The connection profile.</param>
        /// <returns>The SSL mode.</returns>
        private static MySqlSslMode ResolveSslMode(ConnectionProfile profile)
        {
            if (profile.Encrypt == false)
            {
                return MySqlSslMode.None;
            }

            // Preferred uses SSL when the server offers it, without validating the certificate;
            // VerifyFull requires SSL and validates the certificate chain and host name.
            return profile.TrustServerCertificate == true ? MySqlSslMode.Preferred : MySqlSslMode.VerifyFull;
        }
    }
}
